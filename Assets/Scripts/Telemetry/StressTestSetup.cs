using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StressTestSetup : MonoBehaviour
{
    [Header("Options")]
    [SerializeField] private bool staggerPlacement = false;
    [SerializeField] private float staggerRowDelay = 0.05f;

    private MatchManager match;
    private FarmManager farm;
    private GameManager game;

    private bool hasRun = false;

    // NOTE: assumes LevelData exposes a seed prefab list under this name -
    // rename to match whatever GameManager.currentLevel actually calls it.
    private List<GameObject> seedPrefabs;

    private Coroutine staggerRoutine;

    private void Start()
    {
        match = MatchManager.instance;
        farm = FarmManager.instance;
        game = GameManager.instance;

        if (match == null)
        {
            Debug.LogError("[StressTestSetup] MatchManager.instance not found. Aborting.");
            enabled = false;
            return;
        }

        if (farm == null)
        {
            Debug.LogError("[StressTestSetup] FarmManager.instance is null. Aborting.");
            enabled = false;
            return;
        }

        if (game == null)
        {
            Debug.LogError("[StressTestSetup] GameManager.instance is null. Aborting.");
            enabled = false;
            return;
        }

        hasRun = game.currentLevel.setTestFormat;
        seedPrefabs = game.currentLevel.seedPrefabs;
        if (seedPrefabs == null || seedPrefabs.Count == 0)
        {
            Debug.LogError("[StressTestSetup] No seed prefabs on currentLevel. Assign at least one. Aborting.");
            enabled = false;
            return;
        }

        Debug.Log("[StressTestSetup] Ready — waiting for match to start.");
    }

    private void Update()
    {
        if (hasRun) return;
        if (match == null || !match.isMatchRunning) return;

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

    private void PopulateImmediate()
    {
        Debug.Log("[StressTestSetup] Populating farm grid immediately...");

        List<Vector3Int> cells = CollectPreparedCells();
        List<int> players = GetRegisteredPlayerIndices();

        if (players.Count == 0)
        {
            Debug.LogError("[StressTestSetup] No registered players found. Cannot assign plant ownership.");
            return;
        }

        int seedIdx = 0;
        int playerIdx = 0;
        int planted = 0;

        foreach (var cell in cells)
        {
            PlantAt(cell, players[playerIdx % players.Count], seedPrefabs[seedIdx % seedPrefabs.Count]);

            seedIdx++;
            playerIdx++;
            planted++;
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
            if (this == null || match == null || !match.isMatchRunning)
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
            if (farm.IsPrepared(cell) && !farm.IsOccupied(cell))
                prepared.Add(cell);
        }

        Debug.Log($"[StressTestSetup] Found {prepared.Count} prepared cells.");
        return prepared;
    }

    private List<int> GetRegisteredPlayerIndices()
    {
        var result = new List<int>();
        var players = match.Players; // requires the MatchManager.Players accessor noted above

        for (int i = 0; i < players.Count; i++)
            result.Add(i);

        return result;
    }

    private void PlantAt(Vector3Int cell, int playerIndex, GameObject seedPrefab)
    {
        // Local-only: calls FarmManager directly rather than going through a
        // network service. FarmManager's own isServer/isClient checks already
        // fall through cleanly when no Mirror session is active.
        farm.PlantSeed(cell, playerIndex, seedPrefab);
    }
}