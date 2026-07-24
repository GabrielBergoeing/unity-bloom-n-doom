using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class StressTestSetup : MonoBehaviour
{
    [Header("Options")]
    [SerializeField] private bool staggerPlacement = false;
    [SerializeField] private float staggerRowDelay = 0.05f;
    [SerializeField] private float dependencyRetryTimeout = 5f;

    private MatchManager offlineMatch;
    private OnlineMatchManager onlineMatch;
    private bool isOnline;
    private bool isHostCheckDone = false;

    private FarmManager farm;
    private GameManager game;

    private bool initialized = false;
    private bool hasRun = false;
    private float initElapsed = 0f;

    private List<GameObject> seedPrefabs;
    private Coroutine staggerRoutine;

    private void Start()
    {
        // Deferred - see TryInitialize(). Don't resolve/abort on dependencies here;
        // OnlineMatchManager.instance (and possibly FarmManager/GameManager, depending
        // on scene spawn order) may not be set yet at this point even though they will
        // be a few frames later. Mirror's OnStartServer()/OnStartClient() callbacks are
        // not synchronized with any GameObject's own Start().
    }

    private void Update()
    {
        if (!initialized)
        {
            TryInitialize();
            return;
        }

        if (hasRun) return;
        if (!IsMatchRunning()) return;

        Debug.Log("Reached Update");

        hasRun = true;

        if (staggerPlacement)
            staggerRoutine = StartCoroutine(PopulateStaggered());
        else
            PopulateImmediate();
    }

    private void TryInitialize()
    {
        // Resolve isOnline once we can tell for sure. NetworkClient/NetworkServer aren't
        // "active" instantly either, but they flip true synchronously with StartHost/
        // StartClient - unlike OnlineMatchManager.instance, which waits on OnStartServer.
        // Treat "no network session at all yet" as still-undetermined rather than assuming
        // offline, so a slow-starting host doesn't get misclassified in the first frame.
        bool networkActive = NetworkServer.active || NetworkClient.active;

        if (networkActive)
        {
            isOnline = true;

            if (!NetworkServer.active)
            {
                Debug.Log("[StressTestSetup] Online client (non-host) - nothing to do here.");
                enabled = false;
                initialized = true; // stop retrying, this is a real terminal state
                return;
            }

            onlineMatch = OnlineMatchManager.instance;
        }
        else
        {
            offlineMatch = MatchManager.instance;
            // Don't commit to isOnline=false yet on the very first frames - a host that's
            // mid-StartHost() call could still flip NetworkServer.active shortly. Only
            // commit once we've retried a bit and it's still not active (see timeout below).
        }

        farm = FarmManager.instance;
        game = GameManager.instance;

        bool matchReady = networkActive ? onlineMatch != null : offlineMatch != null;

        if (matchReady && farm != null && game != null)
        {
            isOnline = networkActive;
            FinishInit();
            return;
        }

        initElapsed += Time.deltaTime;
        if (initElapsed >= dependencyRetryTimeout)
        {
            // Report exactly what's still missing instead of a generic message.
            var missing = new List<string>();
            if (networkActive ? onlineMatch == null : offlineMatch == null)
                missing.Add(networkActive ? "OnlineMatchManager.instance" : "MatchManager.instance");
            if (farm == null) missing.Add("FarmManager.instance");
            if (game == null) missing.Add("GameManager.instance");

            ToggleDisableError($"Instance dependency not found after {dependencyRetryTimeout}s: {string.Join(", ", missing)}. Aborting.");
            initialized = true;
        }
    }

    private void FinishInit()
    {
        enabled = game.currentLevel.setTestFormat;
        seedPrefabs = game.currentLevel.seedPrefabs;

        if (seedPrefabs == null || seedPrefabs.Count == 0)
        {
            ToggleDisableError("No seed prefabs on currentLevel. Assign at least one. Aborting.");
            initialized = true;
            return;
        }

        initialized = true;
        Debug.Log($"[StressTestSetup] Ready ({(isOnline ? "online/host" : "offline")}) — waiting for match to start.");
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

            if (this == null || !IsMatchRunning())
                yield break;

            foreach (var cell in rows[y])
            {
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
        var players = offlineMatch.Players;

        for (int i = 0; i < players.Count; i++)
            result.Add(i);

        return result;
    }

    private void PlantAt(Vector3Int cell, int playerIndex, GameObject seedPrefab)
    {
        farm.PlantSeed(cell, playerIndex, seedPrefab);
    }

    private void ToggleDisableError(string reason)
    {
        Debug.LogError($"[StressTestSetup] {reason}");
        enabled = false;
    }
}