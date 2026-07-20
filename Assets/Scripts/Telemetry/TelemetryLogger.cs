using System;
using System.Diagnostics;
using System.IO;
using Mirror;
using UnityEngine;
using UnityEngine.Profiling;
using Debug = UnityEngine.Debug;
using System.Globalization;

/// <summary>
/// Ported from the reference Godot TelemetryLogger (core/TelemetryLogger.cs) - same CSV
/// columns and GPU-provider architecture, adapted to Unity APIs. Streams one row per sample
/// straight to disk (like the Godot version's FileAccess.StoreLine) so a crash mid-match
/// still leaves partial data, instead of buffering everything and exporting once at the end.
/// Start/stop is driven externally by MatchManager around the match's actual start and end.
///
/// process_time / physics_time / draw_calls have no built-in Unity equivalent without the
/// com.unity.profiling.core package (not installed in this project) - they're written as -1
/// rather than a fabricated number. fps/frame_time_ms/memory_mb/object_count/node_count are
/// real readings from APIs already confirmed available here.
/// </summary>
public class TelemetryLogger : MonoBehaviour
{
    public static TelemetryLogger instance;

    [Header("Sampling")]
    [SerializeField] private float sampleInterval = 0.25f;
    [SerializeField] private float flushInterval = 5.0f;

    private Process currentProcess;
    private TimeSpan lastCpuTime;
    private DateTime lastCpuSampleWall;

    private IGPUProvider gpuProvider;

    private StreamWriter writer;
    private float timer;
    private float flushTimer;
    private bool isCapturing;

    private string captureName;
    private string absolutePath;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;

        gpuProvider = GPUProviderFactory.Create();
        Debug.Log($"[TelemetryLogger] GPU provider set as: {gpuProvider.GetGPUName()}");
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;

        // Safety net for early exit (e.g. quitting to the main menu mid-match), which
        // changes scenes without ever going through MatchManager.EndMatch()/StopCapture().
        StopCapture();
    }

    private void Update()
    {
        if (!isCapturing || writer == null)
            return;

        flushTimer += Time.unscaledDeltaTime;
        if (flushTimer >= flushInterval)
        {
            flushTimer = 0f;
            writer.Flush();
        }

        timer += Time.unscaledDeltaTime;
        if (timer < sampleInterval)
            return;
        timer = 0f;

        writer.WriteLine(GetLatestRowData());
    }

    public void StartCapture()
    {
        if (isCapturing) return;

        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        absolutePath = DeterminePath(timestamp);

        try
        {
            writer = new StreamWriter(absolutePath, append: false);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[TelemetryLogger] Failed to create telemetry file: {absolutePath} ({ex.Message})");
            return;
        }

        writer.WriteLine(
            "timestamp," +
            "fps," +
            "cpu_usage," +
            "gpu_usage," +
            "frame_time_ms," +
            "process_time," +
            "physics_time," +
            "memory_mb," +
            "object_count," +
            "node_count," +
            "draw_calls," +
            "latency_ms"
        );

        timer = 0f;
        flushTimer = 0f;
        isCapturing = true;
        currentProcess = Process.GetCurrentProcess();
        lastCpuTime = currentProcess.TotalProcessorTime;
        lastCpuSampleWall = DateTime.Now;

        Debug.Log($"[TelemetryLogger] Capture started: {absolutePath}");
    }

    // Manual stop function
    public void StopCapture()
    {
        if (!isCapturing) return;

        isCapturing = false;
        if (writer != null)
        {
            writer.Flush();
            writer.Close();
            writer = null;
        }

        Debug.Log($"[TelemetryLogger] Capture ended: {captureName}");
    }

    private string DeterminePath(string timestamp)
    {
        captureName = $"match_{timestamp}";

        // Mirrors the Godot version's res:// (project-adjacent, easy to find in dev) vs
        // user:// (appdata, for shipped builds) split.
        string baseDir = Application.isEditor
            ? Path.Combine(Application.dataPath, "..", "Telemetry")
            : Path.Combine(Application.persistentDataPath, "Telemetry");

        Directory.CreateDirectory(baseDir);
        return Path.GetFullPath(Path.Combine(baseDir, $"{captureName}.csv"));
    }

    private float GetCpuUsagePercent()
    {
        currentProcess.Refresh();

        DateTime now = DateTime.Now;
        TimeSpan cpuTime = currentProcess.TotalProcessorTime;

        double cpuUsedMs = (cpuTime - lastCpuTime).TotalMilliseconds;
        double elapsedMs = (now - lastCpuSampleWall).TotalMilliseconds;

        lastCpuTime = cpuTime;
        lastCpuSampleWall = now;

        if (elapsedMs <= 0)
            return 0f;

        return (float)(cpuUsedMs / (elapsedMs * Environment.ProcessorCount) * 100.0);
    }

    private string GetLatestRowData()
    {
        ulong timestamp = (ulong)(Time.realtimeSinceStartupAsDouble * 1000.0);

        float fps = Time.unscaledDeltaTime > 0f ? 1f / Time.unscaledDeltaTime : 0f;
        float frameTimeMs = Time.unscaledDeltaTime * 1000f;

        float cpuUsage = GetCpuUsagePercent();
        float gpuUsage = gpuProvider.GetGPUUsagePercent();

        // No built-in Unity API for these without the com.unity.profiling.core package.
        const float processTime = -1f;
        const float physicsTime = -1f;
        const float drawCalls = -1f;

        double memoryMb = Profiler.GetTotalAllocatedMemoryLong() / (1024.0 * 1024.0);

        int nodeCount = FindObjectsByType<GameObject>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            ).Length;
        int objectCount = Resources.FindObjectsOfTypeAll<UnityEngine.Object>().Length;

        string latency = "N/A";
        if (NetworkClient.active && NetworkClient.isConnected)
            latency = (NetworkTime.rtt * 1000.0).ToString("F1");

        /*return
            $"{timestamp}," +
            $"{fps:F1}," +
            $"{cpuUsage:F2}," +
            $"{gpuUsage:F2}," +
            $"{frameTimeMs:F2}," +
            $"{processTime:F4}," +
            $"{physicsTime:F4}," +
            $"{memoryMb:F2}," +
            $"{objectCount}," +
            $"{nodeCount}," +
            $"{drawCalls}," +
            $"{latency}";*/

        return string.Join(",",
            timestamp,
            fps.ToString("F1", CultureInfo.InvariantCulture),
            cpuUsage.ToString("F2", CultureInfo.InvariantCulture),
            gpuUsage.ToString("F2", CultureInfo.InvariantCulture),
            frameTimeMs.ToString("F2", CultureInfo.InvariantCulture),
            processTime.ToString("F4", CultureInfo.InvariantCulture),
            physicsTime.ToString("F4", CultureInfo.InvariantCulture),
            memoryMb.ToString("F2", CultureInfo.InvariantCulture),
            objectCount,
            nodeCount,
            drawCalls,
            latency
        );
    }
}
