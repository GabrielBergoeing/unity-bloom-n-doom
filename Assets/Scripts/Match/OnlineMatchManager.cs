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

        Debug.Log($"[OnlineMatchManager] Match started. Duration: {syncedTimer}s");
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
        FindObjectOfType<UI_MatchResults>(true)?.ShowResults(results);
    }
}
