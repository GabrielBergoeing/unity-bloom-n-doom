using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using Mirror;

/// <summary>
/// Monitor de m�tricas de red (RTT / jitter / p�rdida aproximada) y HUD en pantalla.
/// - Colocar este componente en el prefab del `Player` (tiene que ser un NetworkBehaviour con autoridad del cliente).
/// - S�lo se inicia para el `localPlayer`.
/// - Mide RTT enviando pings al servidor v�a [Command] y recibiendo la respuesta por [TargetRpc].
/// </summary>
public class NetworkMetrics : NetworkBehaviour
{
    [Header("Ping settings")]
    [Tooltip("Intervalo entre pings (segundos)")]
    public float pingInterval = 0.5f;

    [Tooltip("N�mero m�ximo de pings a mantener en la ventana de c�lculo")]
    public int maxWindow = 50;

    [Tooltip("Mostrar HUD al iniciar (puedes ocultarlo con ToggleHUD)")]
    public bool showHUD = true;

    [Header("CSV Export")]
    [Tooltip("Exportar automáticamente al desconectar")]
    public bool autoExportOnStop = true;

    [Tooltip("Tecla para exportar CSV manualmente")]
    public KeyCode exportKey = KeyCode.F8;

    [Tooltip("Tecla para iniciar/detener la captura de métricas")]
    public KeyCode toggleKey = KeyCode.F7;

    [Header("Métricas centralizadas (Tools/SignalingServer)")]
    [Tooltip("Además de guardar el CSV local, lo sube al servidor de señalización (mismo host que HolePunchClient, puerto HTTP separado) para tenerlo todo junto en un solo lugar. Corre para cualquier sesión online (P2P y GameLift por igual, ya que comparten el mismo Network Manager) - no-op solo si no hay HolePunchClient configurado en la escena.")]
    [SerializeField] private ushort metricsUploadPort = 9051;

    // UI
    private GameObject canvasGO;
    private Text infoText;

    // ping window
    private class PingEntry
    {
        public double sendTime;
        public bool received;
        public double rttMs;
    }

    private readonly Queue<PingEntry> window = new();

    // CSV sample buffer
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
        public string matchId;
        public int playerSlot;
    }

    private readonly List<MetricSample> csvBuffer = new();
    private string lastExportPath = null;

    private Coroutine pingCoroutine;
    private bool isRecording = false;
    private Player ownerPlayer;

    // stats cached
    private double lastRtt = 0;
    private double avgRtt = 0;
    private double jitterMs = 0;
    private double lossPercent = 0;

    void Awake()
    {
        ownerPlayer = GetComponent<Player>();
    }

    // Empty until a match's OnlineMatchManager.OnStartServer() generates one - every player
    // (host + all clients) reads the same synced value, so files from the same match share
    // an identical, exact-match-groupable string instead of relying on overlapping timestamps.
    private string CurrentMatchId => OnlineMatchManager.instance != null ? OnlineMatchManager.instance.matchId : "";
    private int CurrentPlayerSlot => ownerPlayer != null ? ownerPlayer.OwnerIndex : -1;

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();

        CreateHud();
        // Always record - a match's data shouldn't depend on remembering to press
        // toggleKey before the timer starts. StopRecording()/toggleKey are still there
        // for manually pausing capture mid-match if needed.
        StartRecording();
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        StopAndCleanup();
    }

    void OnDestroy()
    {
        StopAndCleanup();
    }

    public void StartRecording()
    {
        if (isRecording) return;
        isRecording = true;
        pingCoroutine = StartCoroutine(PingLoop());
        Debug.Log("[NetworkMetrics] Recording started.");
        UpdateHud();
    }

    public void StopRecording()
    {
        if (!isRecording) return;
        isRecording = false;
        if (pingCoroutine != null)
        {
            StopCoroutine(pingCoroutine);
            pingCoroutine = null;
        }
        Debug.Log("[NetworkMetrics] Recording stopped.");
        UpdateHud();
    }

    void StopAndCleanup()
    {
        if (pingCoroutine != null)
        {
            StopCoroutine(pingCoroutine);
            pingCoroutine = null;
        }
        isRecording = false;

        if (autoExportOnStop && csvBuffer.Count > 0)
            ExportToCsv();

        if (canvasGO != null)
        {
            Destroy(canvasGO);
            canvasGO = null;
            infoText = null;
        }
    }

    void Update()
    {
        if (!isLocalPlayer) return;
        if (Input.GetKeyDown(toggleKey))
        {
            if (isRecording) StopRecording();
            else StartRecording();
        }
        if (Input.GetKeyDown(exportKey))
            ExportToCsv();
    }

    void CreateHud()
    {
        if (!showHUD) return;

        canvasGO = new GameObject("NetworkMetricsCanvas");
        DontDestroyOnLoad(canvasGO);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;

        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        var textGO = new GameObject("NetworkMetricsText");
        textGO.transform.SetParent(canvasGO.transform, false);

        infoText = textGO.AddComponent<Text>();

        // Intentamos cargar la fuente incorporada compatible con Unity moderno.
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
        {
            // Fallback: crear una fuente din�mica a partir de fuentes del sistema.
            font = Font.CreateDynamicFontFromOSFont(new[] { "Arial", "Segoe UI", "Roboto", "Sans Serif" }, 14);
        }

        infoText.font = font;
        infoText.fontSize = 14;
        infoText.alignment = TextAnchor.UpperLeft;
        infoText.horizontalOverflow = HorizontalWrapMode.Overflow;
        infoText.verticalOverflow = VerticalWrapMode.Overflow;
        infoText.color = Color.white;

        var rt = infoText.rectTransform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(10f, -10f);
        rt.sizeDelta = new Vector2(420f, 200f);
    }

    IEnumerator PingLoop()
    {
        while (true)
        {
            SendPing();
            yield return new WaitForSeconds(pingInterval);
        }
    }

    void SendPing()
    {
        if (!isLocalPlayer || !isClient) return;

        double t = Time.realtimeSinceStartupAsDouble;

        // push entry
        var entry = new PingEntry { sendTime = t, received = false, rttMs = 0 };
        window.Enqueue(entry);
        while (window.Count > maxWindow) window.Dequeue();

        // call server
        CmdSendPing(t);
    }

    // Command sent from client -> server
    [Command]
    void CmdSendPing(double clientSendTime)
    {
        // Immediately reply to the client that sent this command.
        // connectionToClient is the client's connection for this call.
        TargetPong(connectionToClient, clientSendTime, Time.realtimeSinceStartupAsDouble);
    }

    // TargetRpc (server -> single client)
    [TargetRpc]
    void TargetPong(NetworkConnection target, double clientSendTime, double serverReceiveTime)
    {
        if (!isLocalPlayer) return;

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

        ComputeStats();
        RecordSample();
        UpdateHud();
    }

    void ComputeStats()
    {
        // compute avg, jitter (std dev), loss over current window
        int total = window.Count;
        if (total == 0)
        {
            lastRtt = avgRtt = jitterMs = lossPercent = 0;
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

        // jitter: sample standard deviation of rtts
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

    void RecordSample()
    {
        string addr = NetworkManager.singleton != null && !string.IsNullOrWhiteSpace(NetworkManager.singleton.networkAddress)
            ? NetworkManager.singleton.networkAddress
            : "(no address)";
        string mode = NetworkServer.active ? "Host/Server" : (NetworkClient.isConnected ? "Client" : "Offline");

        csvBuffer.Add(new MetricSample
        {
            timestamp    = DateTime.Now,
            rttLastMs    = lastRtt,
            rttAvgMs     = avgRtt,
            jitterMs     = jitterMs,
            lossPercent  = lossPercent,
            windowSize   = window.Count,
            mode         = mode,
            serverAddress = addr,
            matchId      = CurrentMatchId,
            playerSlot   = CurrentPlayerSlot
        });
    }

    /// <summary>
    /// Exports all buffered samples to a CSV file in Application.persistentDataPath.
    /// Can also be called from editor scripts or tests.
    /// </summary>
    public string ExportToCsv()
    {
        if (csvBuffer.Count == 0)
        {
            Debug.LogWarning("[NetworkMetrics] No samples to export.");
            return null;
        }

        // Filename-safe short tag ("Host/Server" has a slash, which breaks paths on Windows).
        // Whoever ends up with everyone's CSVs afterwards can now group them by the exact
        // "_Match-<id>_" substring instead of guessing from overlapping timestamps.
        string modeTag = NetworkServer.active ? "Host" : (NetworkClient.isConnected ? "Client" : "Offline");
        string matchId = CurrentMatchId;
        string slotTag = CurrentPlayerSlot >= 0 ? $"_P{CurrentPlayerSlot}" : "";

        string dir = Application.persistentDataPath;
        string fileName = string.IsNullOrEmpty(matchId)
            ? $"NetworkMetrics_{DateTime.Now:yyyyMMdd_HHmmss}_{modeTag}{slotTag}.csv"
            : $"NetworkMetrics_Match-{matchId}_{modeTag}{slotTag}.csv";
        string path = Path.Combine(dir, fileName);

        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("Timestamp,Mode,ServerAddress,RTT_Last_ms,RTT_Avg_ms,Jitter_ms,Loss_pct,WindowSize,MatchId,PlayerSlot");

            foreach (var s in csvBuffer)
            {
                // Always InvariantCulture - on a machine whose Windows region uses comma as
                // the decimal separator (common in es-* locales), plain .ToString("F2") here
                // would silently write "106,81" instead of "106.81", which splits into two
                // extra CSV columns and corrupts every field after it for that row.
                sb.AppendLine(string.Join(",",
                    s.timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture),
                    EscapeCsv(s.mode),
                    EscapeCsv(s.serverAddress),
                    s.rttLastMs.ToString("F2", CultureInfo.InvariantCulture),
                    s.rttAvgMs.ToString("F2", CultureInfo.InvariantCulture),
                    s.jitterMs.ToString("F2", CultureInfo.InvariantCulture),
                    s.lossPercent.ToString("F2", CultureInfo.InvariantCulture),
                    s.windowSize.ToString(CultureInfo.InvariantCulture),
                    EscapeCsv(s.matchId),
                    s.playerSlot.ToString(CultureInfo.InvariantCulture)));
            }

            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            lastExportPath = path;
            Debug.Log($"[NetworkMetrics] Exported {csvBuffer.Count} samples -> {path}");

            StartCoroutine(UploadToSignalingServer(sb.ToString()));

            // Match end (RpcShowResults) and the later OnStopClient/OnDestroy safety-net
            // export both call this - without clearing, the second call re-exports the
            // exact same samples under a new filename/timestamp, producing a near-duplicate
            // CSV every match. Clearing here makes repeat calls no-ops (empty buffer) unless
            // new samples were actually recorded since the last export.
            csvBuffer.Clear();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[NetworkMetrics] CSV export failed: {ex.Message}");
            return null;
        }

        return path;
    }

    // Best-effort: only runs when a HolePunchClient is present and configured, since
    // that's how this script finds the signaling server's address - it doesn't actually
    // check HOW the connection was made. HolePunchClient lives on the same shared Network
    // Manager object every online path uses (P2P and GameLift alike, since GameLift
    // clients also pass through CharacterSelectorOnline.unity), so this also runs for
    // GameLift sessions in practice, not just direct P2P ones. Silently no-ops for plain
    // LAN testing (no HolePunchClient configured there) - the local CSV file is written
    // either way regardless of this upload's success.
    private IEnumerator UploadToSignalingServer(string csvContent)
    {
        var holePunch = FindFirstObjectByType<HolePunchClient>();
        if (holePunch == null || !holePunch.IsConfigured)
            yield break;

        // Join codes give us a room code; GameLift sessions don't (no join code involved),
        // so fall back to the match's own GameLift-assigned matchId for those - without
        // this every GameLift match's upload was indistinguishably labeled "sin-sala".
        string room = !string.IsNullOrEmpty(SteamLobby.ActiveRoomCode)
            ? SteamLobby.ActiveRoomCode
            : (!string.IsNullOrEmpty(CurrentMatchId) ? CurrentMatchId : "sin-sala");
        string role = NetworkServer.active ? "Host" : "Client";
        string player = netId.ToString();

        string url = $"http://{holePunch.SignalingServerHost}:{metricsUploadPort}/metrics" +
                     $"?room={UnityWebRequest.EscapeURL(room)}&role={UnityWebRequest.EscapeURL(role)}&player={UnityWebRequest.EscapeURL(player)}";

        using UnityWebRequest request = new UnityWebRequest(url, "POST")
        {
            uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(csvContent)),
            downloadHandler = new DownloadHandlerBuffer()
        };
        request.SetRequestHeader("Content-Type", "text/csv");

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
            Debug.Log("[NetworkMetrics] CSV subido al servidor de señalización.");
        else
            Debug.LogWarning($"[NetworkMetrics] No se pudo subir el CSV al servidor de señalización: {request.error}");
    }

    private static string EscapeCsv(string value)
    {
        if (value == null) return "";
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }

    void UpdateHud()
    {
        if (infoText == null) return;

        // Use NetworkManager singleton's networkAddress (stable across Mirror versions)
        string addr = NetworkManager.singleton != null && !string.IsNullOrWhiteSpace(NetworkManager.singleton.networkAddress)
            ? NetworkManager.singleton.networkAddress
            : "(no address)";

        // Tambi�n mostrar el id de conexi�n si est� disponible
        // Mirror no expone connectionId p�blicamente en NetworkConnectionToServer.
        // Usamos NetworkConnection.LocalConnectionId si es local, o mostramos "(no disponible)".
        string connIdStr = "(no disponible)";
        if (NetworkClient.connection != null)
        {
            // Si es una conexi�n local, usamos el valor constante.
            if (NetworkClient.connection is NetworkConnectionToServer)
                connIdStr = NetworkConnection.LocalConnectionId.ToString();
        }

        string mode = NetworkServer.active ? "Host/Server" : (NetworkClient.isConnected ? "Client" : "Offline");

        string recStatus = isRecording ? "RECORDING" : "STOPPED";

        infoText.text =
            $"Network metrics ({mode})  [{recStatus}]\n" +
            $"Server: {addr} (connId: {connIdStr})\n\n" +
            $"RTT last: {lastRtt:F1} ms\n" +
            $"RTT avg:  {avgRtt:F1} ms\n" +
            $"Jitter:   {jitterMs:F1} ms\n" +
            $"Loss:     {lossPercent:F1} %\n" +
            $"Window:   {window.Count} samples  |  Recorded: {csvBuffer.Count}\n\n" +
            $"Ping interval: {pingInterval:F2}s\n" +
            $"SentRate (NM): {NetworkManager.singleton?.sendRate ?? 0} Hz\n" +
            $"Time: {DateTime.Now:HH:mm:ss}\n" +
            $"[{toggleKey}] {(isRecording ? "Stop" : "Start")} recording  |  [{exportKey}] Export CSV" +
            (lastExportPath != null ? $"\nLast export: {Path.GetFileName(lastExportPath)}" : "");
    }

    // utilidad p�blica para alternar HUD en runtime
    public void ToggleHUD(bool enabled)
    {
        showHUD = enabled;
        if (canvasGO != null) canvasGO.SetActive(enabled);
        else if (enabled && isLocalPlayer) CreateHud();
    }
}