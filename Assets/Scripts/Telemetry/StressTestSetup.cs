using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Automated bot-driven load test: once a setTestFormat level's match starts, auto-plants
/// seeds across every preparable cell so match/network load can be exercised without manual
/// play. Online/host-only by design - GameSession only ever creates this on the server (see
/// GameSession.SpawnPlayersAndStartMatch()), so unlike the implementation this was ported
/// from, there's no client-only peer case to detect here: this component's mere existence
/// already means we're running on the host.
/// </summary>
public class StressTestSetup : MonoBehaviour
{
    [Header("Options")]
    [SerializeField] private bool staggerPlacement = false;
    [SerializeField] private float staggerRowDelay = 0.05f;

    private FarmManager farm;
    private GameManager game;
    private List<GameObject> seedPrefabs;

    private bool hasRun = false;
    private Coroutine staggerRoutine;

    private void Start()
    {
        farm = FarmManager.instance;
        game = GameManager.instance;

        if (farm == null || game == null || game.currentLevel == null)
        {
            ToggleDisableError("FarmManager/GameManager/currentLevel not ready. Aborting.");
            return;
        }

        seedPrefabs = game.currentLevel.seedPrefabs;
        if (seedPrefabs == null || seedPrefabs.Count == 0)
        {
            ToggleDisableError("No seed prefabs on currentLevel. Assign at least one. Aborting.");
            return;
        }

        Debug.Log("[StressTestSetup] Ready (online/host) — waiting for match to start.");
    }

    private void Update()
    {
        if (hasRun) return;
        if (!IsMatchRunning()) return;

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

    private bool IsMatchRunning() =>
        GameSession.Instance != null && GameSession.Instance.State == GameSession.SessionState.Playing;

    private void PopulateImmediate()
    {
        List<Vector3Int> cells = CollectPreparedCells();
        List<int> players = GetRegisteredPlayerIndices();

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
            var prefab = seed.GetComponent<Seed>().PlantPrefab;

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
                var seed = seedPrefabs[seedIdx % seedPrefabs.Count];
                var prefab = seed.GetComponent<Seed>().PlantPrefab;
                PlantAt(cell, players[playerIdx % players.Count], prefab);

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
            GameSession.Instance.ServerTryPrepareTile(cell);
            if (farm.IsPrepared(cell) && !farm.IsOccupied(cell))
                prepared.Add(cell);
        }

        Debug.Log($"[StressTestSetup] Found {prepared.Count} prepared cells.");
        return prepared;
    }

    // Player index assignment mirrors GameSession.SpawnPlayersAndStartMatch(), which
    // hands out 0..N-1 in lobbyPlayers order - so round-robining over that same range
    // gives ownership indices real players could also have.
    private List<int> GetRegisteredPlayerIndices()
    {
        var result = new List<int>();
        int count = GameSession.Instance.lobbyPlayers.Count;
        for (int i = 0; i < count; i++)
            result.Add(i);
        return result;
    }

    private void PlantAt(Vector3Int cell, int playerIndex, GameObject plantPrefab)
    {
        GameSession.Instance.ServerTryPlantSeed(cell, plantPrefab, playerIndex);
    }

    private void ToggleDisableError(string reason)
    {
        Debug.LogError($"[StressTestSetup] {reason}");
        enabled = false;
    }
}
