using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Mirror;

/// <summary>
/// Monitor de métricas de red (RTT / jitter / pérdida aproximada) y HUD en pantalla.
/// - Colocar este componente en el prefab del `Player` (tiene que ser un NetworkBehaviour con autoridad del cliente).
/// - Sólo se inicia para el `localPlayer`.
/// - Mide RTT enviando pings al servidor vía [Command] y recibiendo la respuesta por [TargetRpc].
/// </summary>
public class NetworkMetrics : NetworkBehaviour
{
    [Header("Ping settings")]
    [Tooltip("Intervalo entre pings (segundos)")]
    public float pingInterval = 0.5f;

    [Tooltip("Número máximo de pings a mantener en la ventana de cálculo")]
    public int maxWindow = 50;

    [Tooltip("Mostrar HUD al iniciar (puedes ocultarlo con ToggleHUD)")]
    public bool showHUD = true;

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

    private Coroutine pingCoroutine;

    // stats cached
    private double lastRtt = 0;
    private double avgRtt = 0;
    private double jitterMs = 0;
    private double lossPercent = 0;

    void Awake()
    {
        // nothing here - only start on local player
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();

        CreateHud();
        pingCoroutine = StartCoroutine(PingLoop());
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

    void StopAndCleanup()
    {
        if (pingCoroutine != null)
        {
            StopCoroutine(pingCoroutine);
            pingCoroutine = null;
        }

        if (canvasGO != null)
        {
            Destroy(canvasGO);
            canvasGO = null;
            infoText = null;
        }
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
            // Fallback: crear una fuente dinámica a partir de fuentes del sistema.
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

    void UpdateHud()
    {
        if (infoText == null) return;

        // Use NetworkManager singleton's networkAddress (stable across Mirror versions)
        string addr = NetworkManager.singleton != null && !string.IsNullOrWhiteSpace(NetworkManager.singleton.networkAddress)
            ? NetworkManager.singleton.networkAddress
            : "(no address)";

        // También mostrar el id de conexión si está disponible
        // Mirror no expone connectionId públicamente en NetworkConnectionToServer.
        // Usamos NetworkConnection.LocalConnectionId si es local, o mostramos "(no disponible)".
        string connIdStr = "(no disponible)";
        if (NetworkClient.connection != null)
        {
            // Si es una conexión local, usamos el valor constante.
            if (NetworkClient.connection is NetworkConnectionToServer)
                connIdStr = NetworkConnection.LocalConnectionId.ToString();
        }

        string mode = NetworkServer.active ? "Host/Server" : (NetworkClient.isConnected ? "Client" : "Offline");

        infoText.text =
            $"Network metrics ({mode})\n" +
            $"Server: {addr} (connId: {connIdStr})\n\n" +
            $"RTT last: {lastRtt:F1} ms\n" +
            $"RTT avg:  {avgRtt:F1} ms\n" +
            $"Jitter:   {jitterMs:F1} ms\n" +
            $"Loss:     {lossPercent:F1} %\n" +
            $"Window:   {window.Count} samples\n\n" +
            $"Ping interval: {pingInterval:F2}s\n" +
            $"SentRate (NM): {NetworkManager.singleton?.sendRate ?? 0} Hz\n" +
            $"Time: {DateTime.Now:HH:mm:ss}";
    }

    // utilidad pública para alternar HUD en runtime
    public void ToggleHUD(bool enabled)
    {
        showHUD = enabled;
        if (canvasGO != null) canvasGO.SetActive(enabled);
        else if (enabled && isLocalPlayer) CreateHud();
    }
}