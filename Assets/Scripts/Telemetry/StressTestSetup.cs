using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class StressTestSetup : MonoBehaviour
{
    [Header("Options")]
    [SerializeField] private bool staggerPlacement = false;
    [SerializeField] private float staggerRowDelay = 0.05f;

    private MatchManager offlineMatch;
    private OnlineMatchManager onlineMatch;
    private bool isOnline;

    private FarmManager farm;
    private GameManager game;

    private bool hasRun = false;

    // NOTE: assumes LevelData exposes a seed prefab list under this name -
    // rename to match whatever GameManager.currentLevel actually calls it.
    private List<GameObject> seedPrefabs;

    private Coroutine staggerRoutine;

    private void Start()
    {
        onlineMatch = OnlineMatchManager.instance;
        isOnline = onlineMatch != null;

        // Online: only the host should ever run this. FarmManager.PlantSeed already
        // no-ops for non-host clients (isServer gate), but it doesn't return a success/
        // failure signal - so without this early-out, a connected client would still log
        // "Done - N plants placed" even though every PlantSeed call silently did nothing.
        if (isOnline && !NetworkServer.active)
        {
            Debug.Log("[StressTestSetup] Online client (non-host) - nothing to do here.");
            enabled = false;
            return;
        }

        offlineMatch = isOnline ? null : MatchManager.instance;
        farm = FarmManager.instance;
        game = GameManager.instance;

        bool matchMissing = isOnline ? onlineMatch == null : offlineMatch == null;
        if (matchMissing || farm == null || game == null)
        {
            ToggleDisableError("Instance Dependancy not found. Aborting.");
            return;
        }

        enabled = game.currentLevel.setTestFormat;
        seedPrefabs = game.currentLevel.seedPrefabs;

        if (seedPrefabs == null || seedPrefabs.Count == 0)
        {
            ToggleDisableError("No seed prefabs on currentLevel. Assign at least one. Aborting.");
            return;
        }

        Debug.Log("[StressTestSetup] Ready — waiting for match to start.");
    }

    private void Update()
    {
        if (hasRun) return;
        if (!IsMatchRunning()) return;

        Debug.Log("Reached Update");

        hasRun = true;

        if (staggerPlacement)
            staggerRoutine = StartCoroutine(PopulateStaggered());
        else
            PopulateImmediate();
    }

    private void OnDestroy()
    {
        if (staggerRoutine != null)
            StopCoroutine(staggerRoutine);
    }

    private bool IsMatchRunning()
    {
        if (isOnline)
            return onlineMatch != null && onlineMatch.isMatchRunning;

        return offlineMatch != null && offlineMatch.isMatchRunning;
    }

    private void PopulateImmediate()
    {
        List<Vector3Int> cells = CollectPreparedCells();
        List<int> players = GetRegisteredPlayerIndices();

        if (seedPrefabs == null)
        {
            ToggleDisableError("[StressTestSetup] seedPrefabs list is NULL.");
            return;
        }

        if (players.Count == 0 || seedPrefabs.Count == 0)
        {
            ToggleDisableError("No registered players/seeds found.");
            return;
        }

        int seedIdx = 0;
        int playerIdx = 0;
        int planted = 0;

        foreach (var cell in cells)
        {
            int owner = players[playerIdx % players.Count];
            var seed = seedPrefabs[seedIdx % seedPrefabs.Count];
            var prefab = seed.GetComponent<Seed>().plantPrefab;

            PlantAt(cell, owner, prefab);

            planted++;
            seedIdx++;
            playerIdx++;
        }

        Debug.Log($"[StressTestSetup] Done — {planted} plants placed across {cells.Count} prepared cells.");
    }

    private IEnumerator PopulateStaggered()
    {
        Debug.Log("[StressTestSetup] Populating farm grid with stagger...");

        List<Vector3Int> cells = CollectPreparedCells();
        List<int> players = GetRegisteredPlayerIndices();

        if (players.Count == 0)
        {
            Debug.LogError("[StressTestSetup] No registered players found. Cannot assign plant ownership.");
            yield break;
        }

        var rows = new Dictionary<int, List<Vector3Int>>();
        foreach (var cell in cells)
        {
            if (!rows.TryGetValue(cell.y, out var row))
            {
                row = new List<Vector3Int>();
                rows[cell.y] = row;
            }
            row.Add(cell);
        }

        var sortedYs = new List<int>(rows.Keys);
        sortedYs.Sort();

        int seedIdx = 0;
        int playerIdx = 0;
        int planted = 0;

        foreach (int y in sortedYs)
        {
            yield return new WaitForSeconds(staggerRowDelay);

            // Match may have ended, or this component been destroyed, during the wait.
            if (this == null || !IsMatchRunning())
                yield break;

            foreach (var cell in rows[y])
            {
                // NOTE: pre-existing bug, unrelated to online/offline - this passes the
                // raw Seed wrapper prefab through instead of unwrapping .plantPrefab like
                // PopulateImmediate does below. Left as-is here since fixing it wasn't
                // part of this pass; flagging again so it doesn't get lost.
                PlantAt(cell, players[playerIdx % players.Count], seedPrefabs[seedIdx % seedPrefabs.Count]);

                seedIdx++;
                playerIdx++;
                planted++;
            }
        }

        Debug.Log($"[StressTestSetup] Done (staggered) — {planted} plants placed.");
    }

    private List<Vector3Int> CollectPreparedCells()
    {
        var prepared = new List<Vector3Int>();
        BoundsInt bounds = farm.farmTilemap.cellBounds;

        foreach (var cell in bounds.allPositionsWithin)
        {
            farm.PrepareTile(cell);
            if (farm.IsPrepared(cell) && !farm.IsOccupied(cell))
                prepared.Add(cell);
        }

        Debug.Log($"[StressTestSetup] Found {prepared.Count} prepared cells.");
        return prepared;
    }

    private List<int> GetRegisteredPlayerIndices()
    {
        if (isOnline)
            return new List<int>(onlineMatch.PlayerIndices);

        var result = new List<int>();
        var players = offlineMatch.Players; // requires the MatchManager.Players accessor noted above

        for (int i = 0; i < players.Count; i++)
            result.Add(i);

        return result;
    }

    private void PlantAt(Vector3Int cell, int playerIndex, GameObject seedPrefab)
    {
        // Local-only: calls FarmManager directly rather than going through a
        // network service. FarmManager's own isServer/isClient checks already
        // fall through cleanly when no Mirror session is active, and this method
        // is now only ever reached on the host in the online case (see Start()).
        farm.PlantSeed(cell, playerIndex, seedPrefab);
    }

    private void ToggleDisableError(string reason)
    {
        Debug.LogError($"[StressTestSetup] {reason}");
        enabled = false;
    }
}