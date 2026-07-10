using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Profiling;
using Debug = UnityEngine.Debug;

/// <summary>
/// System-performance monitor (FPS / frame time / CPU / memory) + HUD for offline local matches.
/// - Plain MonoBehaviour singleton, does not require Mirror/NetworkBehaviour.
/// - Recording is started/stopped by MatchManager around InitializePlayers()/EndMatch(),
///   so each CSV covers exactly one match.
/// Mirrors the NetworkMetrics.cs conventions (CSV export, HUD, toggle/export hotkeys).
/// </summary>
public class OfflineMetrics : MonoBehaviour
{
    public static OfflineMetrics instance;

    [Header("Sampling settings")]
    [Tooltip("Interval between samples (seconds)")]
    public float sampleInterval = 0.5f;

    [Tooltip("Show HUD while recording")]
    public bool showHUD = true;

    [Header("CSV Export")]
    [Tooltip("Export automatically when recording stops")]
    public bool autoExportOnStop = true;

    [Tooltip("Manual export key")]
    public KeyCode exportKey = KeyCode.F8;

    [Tooltip("Manual start/stop recording key")]
    public KeyCode toggleKey = KeyCode.F7;

    // UI
    private GameObject canvasGO;
    private Text infoText;

    private struct MetricSample
    {
        public DateTime timestamp;
        public float fps;
        public float frameTimeMs;
        public float cpuUsagePercent;
        public long managedMemoryMB;
        public long workingSetMemoryMB;
        public string levelName;
    }

    private readonly List<MetricSample> csvBuffer = new();
    private string lastExportPath = null;

    private Coroutine sampleCoroutine;
    private bool isRecording = false;

    // process CPU tracking
    private Process currentProcess;
    private TimeSpan lastCpuTime;
    private DateTime lastCpuSampleWall;

    // cached stats
    private float fps = 0f;
    private float frameTimeMs = 0f;
    private float cpuUsagePercent = 0f;
    private long managedMemoryMB = 0;
    private long workingSetMemoryMB = 0;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        currentProcess = Process.GetCurrentProcess();
        lastCpuTime = currentProcess.TotalProcessorTime;
        lastCpuSampleWall = DateTime.Now;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;

        StopAndCleanup();
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            if (isRecording) StopRecording();
            else StartRecording();
        }
        if (Input.GetKeyDown(exportKey))
            ExportToCsv();
    }

    public void StartRecording()
    {
        if (isRecording) return;
        isRecording = true;

        if (showHUD)
            CreateHud();

        sampleCoroutine = StartCoroutine(SampleLoop());
        Debug.Log("[OfflineMetrics] Recording started.");
    }

    public void StopRecording()
    {
        if (!isRecording) return;
        isRecording = false;

        if (sampleCoroutine != null)
        {
            StopCoroutine(sampleCoroutine);
            sampleCoroutine = null;
        }

        Debug.Log("[OfflineMetrics] Recording stopped.");

        if (autoExportOnStop && csvBuffer.Count > 0)
            ExportToCsv();
    }

    private void StopAndCleanup()
    {
        bool wasRecording = isRecording;

        if (sampleCoroutine != null)
        {
            StopCoroutine(sampleCoroutine);
            sampleCoroutine = null;
        }
        isRecording = false;

        // Covers early exit (e.g. quitting to the main menu mid-match via the pause menu),
        // which changes scenes without ever going through MatchManager.EndMatch()/
        // StopRecording() - without this, the destroyed object's samples would just be
        // lost. Only export here if we were still recording; a normal StopRecording()
        // call already exported, so this avoids writing a duplicate CSV.
        if (wasRecording && autoExportOnStop && csvBuffer.Count > 0)
            ExportToCsv();

        if (canvasGO != null)
        {
            Destroy(canvasGO);
            canvasGO = null;
            infoText = null;
        }
    }

    private IEnumerator SampleLoop()
    {
        while (true)
        {
            TakeSample();
            yield return new WaitForSeconds(sampleInterval);
        }
    }

    private void TakeSample()
    {
        frameTimeMs = Time.unscaledDeltaTime * 1000f;
        fps = Time.unscaledDeltaTime > 0f ? 1f / Time.unscaledDeltaTime : 0f;

        // CPU usage: process time consumed since last sample / (wall time * core count)
        TimeSpan cpuNow = currentProcess.TotalProcessorTime;
        DateTime wallNow = DateTime.Now;
        double cpuDeltaMs = (cpuNow - lastCpuTime).TotalMilliseconds;
        double wallDeltaMs = (wallNow - lastCpuSampleWall).TotalMilliseconds;
        cpuUsagePercent = wallDeltaMs > 0
            ? (float)(cpuDeltaMs / (wallDeltaMs * Environment.ProcessorCount) * 100.0)
            : 0f;
        lastCpuTime = cpuNow;
        lastCpuSampleWall = wallNow;

        managedMemoryMB = Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024);
        currentProcess.Refresh();
        workingSetMemoryMB = currentProcess.WorkingSet64 / (1024 * 1024);

        RecordSample();
        UpdateHud();
    }

    private void RecordSample()
    {
        string level = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        csvBuffer.Add(new MetricSample
        {
            timestamp = DateTime.Now,
            fps = fps,
            frameTimeMs = frameTimeMs,
            cpuUsagePercent = cpuUsagePercent,
            managedMemoryMB = managedMemoryMB,
            workingSetMemoryMB = workingSetMemoryMB,
            levelName = level
        });
    }

    /// <summary>
    /// Exports all buffered samples to a CSV file in Application.persistentDataPath.
    /// </summary>
    public string ExportToCsv()
    {
        if (csvBuffer.Count == 0)
        {
            Debug.LogWarning("[OfflineMetrics] No samples to export.");
            return null;
        }

        string dir = Application.persistentDataPath;
        string fileName = $"OfflineMetrics_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
        string path = Path.Combine(dir, fileName);

        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("Timestamp,Level,FPS,FrameTime_ms,CPU_pct,ManagedMemory_MB,WorkingSet_MB");

            foreach (var s in csvBuffer)
            {
                sb.AppendLine(string.Join(",",
                    s.timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                    EscapeCsv(s.levelName),
                    s.fps.ToString("F1"),
                    s.frameTimeMs.ToString("F2"),
                    s.cpuUsagePercent.ToString("F1"),
                    s.managedMemoryMB.ToString(),
                    s.workingSetMemoryMB.ToString()));
            }

            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            lastExportPath = path;
            Debug.Log($"[OfflineMetrics] Exported {csvBuffer.Count} samples -> {path}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[OfflineMetrics] CSV export failed: {ex.Message}");
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

    private void CreateHud()
    {
        if (canvasGO != null) return;

        canvasGO = new GameObject("OfflineMetricsCanvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;

        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        var textGO = new GameObject("OfflineMetricsText");
        textGO.transform.SetParent(canvasGO.transform, false);

        infoText = textGO.AddComponent<Text>();

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
            font = Font.CreateDynamicFontFromOSFont(new[] { "Arial", "Segoe UI", "Roboto", "Sans Serif" }, 14);

        infoText.font = font;
        infoText.fontSize = 14;
        infoText.alignment = TextAnchor.UpperRight;
        infoText.horizontalOverflow = HorizontalWrapMode.Overflow;
        infoText.verticalOverflow = VerticalWrapMode.Overflow;
        infoText.color = Color.white;

        var rt = infoText.rectTransform;
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-10f, -10f);
        rt.sizeDelta = new Vector2(320f, 160f);
    }

    private void UpdateHud()
    {
        if (infoText == null) return;

        string recStatus = isRecording ? "RECORDING" : "STOPPED";

        infoText.text =
            $"Offline metrics  [{recStatus}]\n\n" +
            $"FPS:        {fps:F1}\n" +
            $"Frame time: {frameTimeMs:F2} ms\n" +
            $"CPU:        {cpuUsagePercent:F1} %\n" +
            $"Managed mem:{managedMemoryMB} MB\n" +
            $"Working set:{workingSetMemoryMB} MB\n\n" +
            $"Recorded: {csvBuffer.Count} samples\n" +
            $"[{toggleKey}] {(isRecording ? "Stop" : "Start")}  |  [{exportKey}] Export" +
            (lastExportPath != null ? $"\nLast export: {Path.GetFileName(lastExportPath)}" : "");
    }

    public void ToggleHUD(bool enabled)
    {
        showHUD = enabled;
        if (canvasGO != null) canvasGO.SetActive(enabled);
        else if (enabled && isRecording) CreateHud();
    }
}
