using System.Collections.Generic;
using System.Linq;
using Mirror;
using UnityEngine;

/// <summary>
/// Online counterpart to MatchManager. Runs as a networked object in LevelSceneOnline.
///
/// • Server owns the countdown timer (SyncVar) — all clients see it.
/// • Players register themselves via OnStartServer/OnStopServer hooks in Player.cs.
/// • Match ends when timer hits 0; server tallies scores and RPCs results to all clients.
/// • Pause simply freezes the timer server-side (no PlayerInput involved).
///
/// Setup: put this component on a NetworkBehaviour prefab in the NetworkManager's
/// Registered Spawnable Prefabs list AND assign it in the NetworkManager's spawn list,
/// OR place it as a scene object in LevelSceneOnline with Network Identity.
/// </summary>
public class OnlineMatchManager : NetworkBehaviour
{
    public static OnlineMatchManager instance;

    private ScoreTally scoreTally;
    private bool hasPrintResults;

    [SyncVar] private float syncedTimer;
    [SyncVar] private bool syncedIsRunning;

    // Shared by every player (host + all clients) so NetworkMetrics can stamp its CSV
    // filename/rows with it - lets whoever collects everyone's exported files afterwards
    // group them by exact string match instead of guessing from overlapping timestamps.
    [SyncVar] public string matchId;

    public float timer => syncedTimer;
    public bool isMatchRunning => syncedIsRunning && !hasPrintResults;

    private readonly List<Player> players = new();

    private void Awake()
    {
        instance = this;
        scoreTally = GetComponent<ScoreTally>();
        if (scoreTally == null)
            scoreTally = gameObject.AddComponent<ScoreTally>();
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        LevelData level = GameManager.instance?.currentLevel;
        syncedTimer   = level != null ? level.matchDuration : 900f;
        syncedIsRunning = true;
        hasPrintResults = false;

        // Human-readable and naturally unique enough for this purpose (two matches starting
        // in the same server-clock second would need to be the same server instance anyway).
        matchId = System.DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");

        Debug.Log($"[OnlineMatchManager] Match started. Duration: {syncedTimer}s, matchId={matchId}");
    }

    // Runs on every client (host included) when this object spawns into their scene -
    // mirrors MatchManager.InitializePlayers()'s local telemetry capture (fps/cpu/gpu/
    // memory), which only ever ran for offline matches. Each machine samples its own
    // stats locally, same as NetworkMetrics does for RTT, so this has to be client-side
    // rather than gated by isServer.
    public override void OnStartClient()
    {
        base.OnStartClient();

        if (TelemetryLogger.instance == null)
            new GameObject("TelemetryLogger").AddComponent<TelemetryLogger>();

        TelemetryLogger.instance.StartCapture();
    }

    [ServerCallback]
    private void Update()
    {
        if (!syncedIsRunning || hasPrintResults) return;

        syncedTimer -= Time.deltaTime;

        if (syncedTimer <= 0f)
            EndMatch();
    }

    // ── Player registration (called by Player.OnStartServer / OnStopServer) ──

    public void RegisterPlayer(Player player)
    {
        if (player != null && !players.Contains(player))
            players.Add(player);
    }

    public void UnregisterPlayer(Player player)
    {
        players.Remove(player);
    }

    // ── Pause ──

    public void PauseMatch(bool pause)
    {
        if (!isServer) return;
        syncedIsRunning = !pause;

        foreach (var player in players)
        {
            if (player == null) continue;

            if (pause)
                player.DisableControl();
            else
                player.EnableControl();
        }
    }

    public void PauseMatch()   => PauseMatch(true);
    public void UnpauseMatch() => PauseMatch(false);

    // ── End match ──

    [Server]
    private void EndMatch()
    {
        hasPrintResults = true;
        PauseMatch(true);

        var playerNames = players
            .Where(p => p != null)
            .ToDictionary(p => p.OwnerIndex, p => p.name);

        List<ScoreResult> results = scoreTally.DeterminePlacementsByIndex(playerNames);
        RpcShowResults(results);

        Debug.Log("[OnlineMatchManager] Match ended, results sent.");
    }

    [ClientRpc]
    private void RpcShowResults(List<ScoreResult> results)
    {
        FindFirstObjectByType<UI_MatchResults>(FindObjectsInactive.Include)?.ShowResults(results);

        // Stop/export right at match end rather than waiting for OnStopClient/OnDestroy -
        // those only fire on disconnect or scene unload, which can happen much later
        // (or not at all if the player lingers on the results screen still connected).
        var metrics = NetworkClient.localPlayer != null
            ? NetworkClient.localPlayer.GetComponent<NetworkMetrics>()
            : null;

        if (metrics != null)
        {
            metrics.StopRecording();
            metrics.ExportToCsv();
        }

        if (TelemetryLogger.instance != null)
            TelemetryLogger.instance.StopCapture();
    }
}
