using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// NGO port of the Mirror-branch NetworkMetrics module (RTT / jitter / loss via an
/// RPC ping-pong, CSV export, on-screen HUD). Lives on the GameSession prefab so it
/// works from the lobby through the whole match on every peer.
///
/// The first 8 CSV columns are IDENTICAL to the Mirror version so results from both
/// frameworks can be analyzed with the same scripts. Appended columns add the
/// state-consistency metrics of the thesis methodology:
///  - Divergence_units: distance between the local player's position and the
///    server's replicated view of it (echoed back in the pong).
///  - Corrections_total / LastCorrection_units: visual snap corrections detected on
///    remote players (frame-to-frame jumps above a threshold).
///  - ActionLatency_ms: time from requesting a farm action (prepare/plant) to the
///    mirrored tile change being applied locally.
///  - BytesSent/BytesRecv: cumulative transport traffic (see NetTrafficCounter for
///    per-transport scope; -1 when the active transport is not instrumented).
///
/// Keys (same as Mirror version): F7 start/stop capture, F8 export CSV.
/// </summary>
public class NetworkMetrics : NetworkBehaviour
{
    [Header("Ping settings")]
    [Tooltip("Intervalo entre pings (segundos)")]
    public float pingInterval = 0.5f;

    [Tooltip("Número máximo de pings a mantener en la ventana de cálculo")]
    public int maxWindow = 50;

    [Tooltip("Mostrar HUD en pantalla")]
    public bool showHUD = true;

    [Header("Consistency metrics")]
    [Tooltip("Salto mínimo (unidades de mundo) entre frames de un jugador remoto para contarlo como corrección visual")]
    public float correctionThreshold = 1.0f;

    [Header("CSV Export")]
    [Tooltip("Exportar automáticamente al desconectar")]
    public bool autoExportOnStop = true;

    [Tooltip("Tecla para exportar CSV manualmente")]
    public KeyCode exportKey = KeyCode.F8;

    [Tooltip("Tecla para iniciar/detener la captura de métricas")]
    public KeyCode toggleKey = KeyCode.F7;

    [Tooltip("Iniciar captura automáticamente al conectar")]
    public bool autoStart = true;

    // ---- ping window ----
    private class PingEntry
    {
        public double sendTime;
        public bool received;
        public double rttMs;
    }

    private readonly Queue<PingEntry> window = new();
    private float pingTimer;

    // ---- CSV sample buffer ----
    private struct MetricSample
    {
        public DateTime timestamp;
        public double rttLastMs;
        public double rttAvgMs;
        public double jitterMs;
        public double lossPercent;
        public int windowSize;
        public string mode;
        public string serverAddress;
        public double divergenceUnits;
        public int correctionsTotal;
        public double lastCorrectionUnits;
        public double actionLatencyMs;
        public long bytesSent;
        public long bytesRecv;
        public int playerCount;
    }

    private readonly List<MetricSample> csvBuffer = new();
    private string lastExportPath;
    private bool isRecording;

    // ---- cached stats ----
    private double lastRtt, avgRtt, jitterMs, lossPercent;
    private double lastDivergence = -1;

    /// <summary>Last computed average RTT in ms, across all peers. Read by TelemetryLogger
    /// instead of deriving its own RTT, since NGO has no simple built-in equivalent to
    /// Mirror's NetworkTime.rtt.</summary>
    public static double LastAvgRttMs { get; private set; }

    // ---- correction detection (remote players) ----
    private readonly Dictionary<NetworkPlayer, Vector3> lastRemotePositions = new();
    private int correctionsTotal;
    private double lastCorrectionMag;

    // ---- action latency (static hooks called from NetworkPlayer / FarmManager) ----
    private static readonly Dictionary<Vector3Int, double> pendingActions = new();
    private static double lastActionLatencyMs = -1;
    private const double PendingActionExpiry = 5.0;

    public static void NoteActionRequested(Vector3Int cell) =>
        pendingActions[cell] = Time.realtimeSinceStartupAsDouble;

    public static void NoteActionApplied(Vector3Int cell)
    {
        if (!pendingActions.TryGetValue(cell, out double sent))
            return;
        pendingActions.Remove(cell);

        double elapsed = Time.realtimeSinceStartupAsDouble - sent;
        if (elapsed < PendingActionExpiry)
            lastActionLatencyMs = elapsed * 1000.0;
    }

    // ======================================================
    //  LIFECYCLE
    // ======================================================
    public override void OnNetworkSpawn()
    {
        if (IsClient && autoStart)
            StartRecording();
    }

    public override void OnNetworkDespawn() => StopAndExport();

    private void OnDestroy() => StopAndExport();

    public void StartRecording()
    {
        if (isRecording) return;
        isRecording = true;
        pingTimer = 0f;
        window.Clear();
        pendingActions.Clear();
        lastActionLatencyMs = -1;
        correctionsTotal = 0;
        lastCorrectionMag = 0;
        lastRemotePositions.Clear();
        Debug.Log("[NetworkMetrics] Recording started.");
    }

    public void StopRecording()
    {
        if (!isRecording) return;
        isRecording = false;
        Debug.Log("[NetworkMetrics] Recording stopped.");
    }

    private void StopAndExport()
    {
        isRecording = false;
        if (autoExportOnStop && csvBuffer.Count > 0)
            ExportToCsv();
    }

    private void Update()
    {
        if (!IsSpawned || !IsClient) return;

        if (Input.GetKeyDown(toggleKey))
        {
            if (isRecording) StopRecording();
            else StartRecording();
        }
        if (Input.GetKeyDown(exportKey))
            ExportToCsv();

        if (!isRecording) return;

        DetectRemoteCorrections();

        pingTimer += Time.unscaledDeltaTime;
        if (pingTimer >= pingInterval)
        {
            pingTimer = 0f;
            SendPing();
        }
    }

    // ======================================================
    //  PING / PONG
    // ======================================================
    private void SendPing()
    {
        double t = Time.realtimeSinceStartupAsDouble;

        window.Enqueue(new PingEntry { sendTime = t, received = false, rttMs = 0 });
        while (window.Count > maxWindow) window.Dequeue();

        PingServerRpc(t);
    }

    [ServerRpc(RequireOwnership = false)]
    private void PingServerRpc(double clientSendTime, ServerRpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;

        // Echo back the server's replicated view of the sender's player, so the
        // client can measure authority-vs-replica divergence without clock sync.
        Vector3 serverView = Vector3.zero;
        bool hasView = false;
        if (NetworkManager.ConnectedClients.TryGetValue(sender, out var cc) && cc.PlayerObject != null)
        {
            serverView = cc.PlayerObject.transform.position;
            hasView = true;
        }

        PongClientRpc(clientSendTime, serverView, hasView, new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { sender } }
        });
    }

    [ClientRpc]
    private void PongClientRpc(double clientSendTime, Vector3 serverViewPos, bool hasView,
                               ClientRpcParams rpcParams = default)
    {
        double now = Time.realtimeSinceStartupAsDouble;
        double rttMsLocal = (now - clientSendTime) * 1000.0;

        // match the oldest outstanding ping that is not yet received (FIFO)
        foreach (var e in window)
        {
            if (!e.received)
            {
                e.received = true;
                e.rttMs = rttMsLocal;
                break;
            }
        }

        var local = NetworkPlayer.LocalPlayer;
        lastDivergence = hasView && local != null
            ? Vector2.Distance(local.transform.position, serverViewPos)
            : -1;

        ComputeStats();
        RecordSample();
    }

    private void ComputeStats()
    {
        int total = window.Count;
        if (total == 0)
        {
            lastRtt = avgRtt = jitterMs = lossPercent = 0;
            LastAvgRttMs = 0;
            return;
        }

        int recv = 0;
        double sum = 0;
        List<double> rtts = new List<double>(total);
        foreach (var e in window)
        {
            if (e.received)
            {
                recv++;
                sum += e.rttMs;
                rtts.Add(e.rttMs);
            }
        }

        lastRtt = rtts.Count > 0 ? rtts[rtts.Count - 1] : 0;
        avgRtt = recv > 0 ? (sum / recv) : 0;
        LastAvgRttMs = avgRtt;

        // jitter: sample standard deviation of rtts (same definition as Mirror version)
        if (rtts.Count > 1)
        {
            double mean = avgRtt;
            double variance = 0;
            for (int i = 0; i < rtts.Count; i++)
            {
                double d = rtts[i] - mean;
                variance += d * d;
            }
            variance /= (rtts.Count - 1);
            jitterMs = Math.Sqrt(variance);
        }
        else
        {
            jitterMs = 0;
        }

        lossPercent = total > 0 ? ((double)(total - recv) / total) * 100.0 : 0;
    }

    // ======================================================
    //  VISUAL CORRECTION DETECTION (remote players)
    // ======================================================
    private void DetectRemoteCorrections()
    {
        foreach (var np in NetworkPlayer.All)
        {
            if (np == null || np.IsOwner || !np.IsSpawned) continue;

            Vector3 pos = np.transform.position;
            if (lastRemotePositions.TryGetValue(np, out Vector3 prev))
            {
                float jump = Vector2.Distance(prev, pos);
                if (jump > correctionThreshold)
                {
                    correctionsTotal++;
                    lastCorrectionMag = jump;
                }
            }
            lastRemotePositions[np] = pos;
        }
    }

    // ======================================================
    //  SAMPLES & EXPORT
    // ======================================================
    private void RecordSample()
    {
        string mode = IsServer ? "Host/Server" : "Client";
        string addr = ConnectionManager.Instance != null ? ConnectionManager.Instance.LastAddress : "(no address)";

        csvBuffer.Add(new MetricSample
        {
            timestamp = DateTime.Now,
            rttLastMs = lastRtt,
            rttAvgMs = avgRtt,
            jitterMs = jitterMs,
            lossPercent = lossPercent,
            windowSize = window.Count,
            mode = mode,
            serverAddress = addr,
            divergenceUnits = lastDivergence,
            correctionsTotal = correctionsTotal,
            lastCorrectionUnits = lastCorrectionMag,
            actionLatencyMs = lastActionLatencyMs,
            bytesSent = NetTrafficCounter.Active ? NetTrafficCounter.Sent : -1,
            bytesRecv = NetTrafficCounter.Active ? NetTrafficCounter.Received : -1,
            playerCount = NetworkManager != null ? NetworkManager.ConnectedClientsIds.Count : -1
        });
    }

    public string ExportToCsv()
    {
        if (csvBuffer.Count == 0)
        {
            Debug.LogWarning("[NetworkMetrics] No samples to export.");
            return null;
        }

        string transport = ConnectionManager.Instance != null
            ? ConnectionManager.Instance.transport.ToString()
            : "Unknown";

        // Tag both the filename and each row with player count, so runs with different
        // player counts (2/3/4-player stress tests, etc.) are easy to tell apart and
        // group together when comparing multiple exported CSVs afterwards.
        int playerCountForName = csvBuffer.Count > 0 ? csvBuffer[csvBuffer.Count - 1].playerCount : -1;
        string playerTag = playerCountForName >= 0 ? $"_{playerCountForName}P" : "";

        string dir = Application.persistentDataPath;
        string fileName = $"NetworkMetrics_NGO_{transport}{playerTag}_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
        string path = Path.Combine(dir, fileName);

        try
        {
            var sb = new StringBuilder();
            // First 8 columns identical to the Mirror-branch module; extensions appended.
            sb.AppendLine("Timestamp,Mode,ServerAddress,RTT_Last_ms,RTT_Avg_ms,Jitter_ms,Loss_pct,WindowSize," +
                          "Framework,Transport,Divergence_units,Corrections_total,LastCorrection_units," +
                          "ActionLatency_ms,BytesSent,BytesRecv,PlayerCount");

            foreach (var s in csvBuffer)
            {
                sb.AppendLine(string.Join(",",
                    s.timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture),
                    EscapeCsv(s.mode),
                    EscapeCsv(s.serverAddress),
                    s.rttLastMs.ToString("F2", CultureInfo.InvariantCulture),
                    s.rttAvgMs.ToString("F2", CultureInfo.InvariantCulture),
                    s.jitterMs.ToString("F2", CultureInfo.InvariantCulture),
                    s.lossPercent.ToString("F2", CultureInfo.InvariantCulture),
                    s.windowSize.ToString(CultureInfo.InvariantCulture),
                    "NGO",
                    EscapeCsv(transport),
                    s.divergenceUnits.ToString("F3", CultureInfo.InvariantCulture),
                    s.correctionsTotal.ToString(CultureInfo.InvariantCulture),
                    s.lastCorrectionUnits.ToString("F3", CultureInfo.InvariantCulture),
                    s.actionLatencyMs.ToString("F2", CultureInfo.InvariantCulture),
                    s.bytesSent.ToString(CultureInfo.InvariantCulture),
                    s.bytesRecv.ToString(CultureInfo.InvariantCulture),
                    s.playerCount.ToString(CultureInfo.InvariantCulture)));
            }

            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            lastExportPath = path;
            Debug.Log($"[NetworkMetrics] Exported {csvBuffer.Count} samples -> {path}");

            // StopAndExport is called from both OnNetworkDespawn and OnDestroy when a
            // session ends; without clearing, the second call would re-export the exact
            // same samples under a new timestamp filename.
            csvBuffer.Clear();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[NetworkMetrics] CSV export failed: {ex.Message}");
            return null;
        }

        return path;
    }

    private static string EscapeCsv(string value)
    {
        if (value == null) return "";
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }

    // ======================================================
    //  HUD
    // ======================================================
    private void OnGUI()
    {
        if (!showHUD || !IsSpawned || !IsClient) return;

        float scale = Mathf.Max(1f, Screen.height / 720f);
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

        float width = 300f;
        float x = Screen.width / scale - width - 15f;

        string transport = ConnectionManager.Instance != null
            ? ConnectionManager.Instance.transport.ToString()
            : "?";
        string mode = IsServer ? "Host/Server" : "Client";
        string recStatus = isRecording ? "RECORDING" : "STOPPED";
        string bytes = NetTrafficCounter.Active
            ? $"{NetTrafficCounter.Sent / 1024} KB out / {NetTrafficCounter.Received / 1024} KB in"
            : "n/a (UTP not instrumented)";

        GUILayout.BeginArea(new Rect(x, 15f, width, 260f), GUI.skin.box);
        GUILayout.Label($"NGO metrics ({mode})  [{recStatus}]");
        GUILayout.Label($"Transport: {transport}");
        GUILayout.Label($"RTT last: {lastRtt:F1} ms   avg: {avgRtt:F1} ms");
        GUILayout.Label($"Jitter: {jitterMs:F1} ms   Loss: {lossPercent:F1} %");
        GUILayout.Label($"Divergence: {(lastDivergence >= 0 ? lastDivergence.ToString("F2") + " u" : "n/a")}");
        GUILayout.Label($"Corrections: {correctionsTotal} (last {lastCorrectionMag:F2} u)");
        GUILayout.Label($"Action latency: {(lastActionLatencyMs >= 0 ? lastActionLatencyMs.ToString("F0") + " ms" : "n/a")}");
        GUILayout.Label($"Traffic: {bytes}");
        GUILayout.Label($"Samples: {csvBuffer.Count}   Window: {window.Count}");
        GUILayout.Label($"[{toggleKey}] {(isRecording ? "Stop" : "Start")}  [{exportKey}] Export CSV");
        GUILayout.EndArea();
    }
}
