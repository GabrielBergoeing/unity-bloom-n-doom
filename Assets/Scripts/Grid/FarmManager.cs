using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Local farm state (tilemaps + dictionaries) kept in sync on every peer.
/// Online, mutation requests are routed to the server through GameSession;
/// the server validates against its own copy and mirrors the result to all
/// clients via the Apply* methods (called from ClientRpcs).
/// </summary>
public class FarmManager : MonoBehaviour
{
    public static FarmManager instance;

    [Header("References")]
    public Tilemap farmTilemap;
    public Tilemap waterTilemap;
    public Tile preparedTile;
    public Tile seedTile;
    [SerializeField] private Transform plantsRoot;

    [Header("DEV: Generate farm")]
    [SerializeField] private bool startFarm = false;

    // --- Tile State Tracking ---
    private readonly Dictionary<Vector3Int, TileState> tileStates = new();
    private readonly Dictionary<Vector3Int, Plant> plantsByCell = new();
    private readonly HashSet<Vector3Int> occupiedCells = new();

    // --- Player Organisation ---
    private readonly Dictionary<int, Transform> playerPlantRoots = new();

    public enum TileState { NotPrepared, Prepared, PlantedSeed }

    private void Awake()
    {
        instance = this;

        if (!startFarm)
            LevelManager.OnLevelLoaded += () => HandleLevelLoaded(); //Subcribe to LevelManager signal and trigger function when invoked
        else
            InitializeTileStates(true);
    }

    private void Start()
    {
        EnsurePlantsRootExists();
    }

    #region Init Helpers
    public void InitializeTileStates(bool clearBefore = false)
    {
        if (clearBefore)
            tileStates.Clear();

        foreach (var pos in farmTilemap.cellBounds.allPositionsWithin)
        {
            if (farmTilemap.HasTile(pos))
                tileStates[pos] = TileState.NotPrepared;
        }
    }

    private void EnsurePlantsRootExists()
    {
        if (plantsRoot == null)
            plantsRoot = new GameObject("Plants").transform;
    }
    #endregion

    #region Level Signal Handler
    private void OnDestroy() => LevelManager.OnLevelLoaded -= HandleLevelLoaded;

    private void HandleLevelLoaded()
    {
        InitializeTileStates(true);
        Debug.Log("FarmManager tile states initialized after level load");

        LevelManager.OnLevelLoaded -= HandleLevelLoaded;
    }
    #endregion

    #region Tile State Queries
    public bool IsPrepared(Vector3Int cell) =>
        tileStates.TryGetValue(cell, out var state) && state == TileState.Prepared;

    public bool IsOccupied(Vector3Int cell) =>
        occupiedCells.Contains(cell);

    public bool HasPlant(Vector3Int cell) =>
        plantsByCell.ContainsKey(cell);

    public int? GetPlantOwner(Vector3Int cell)
    {
        if (plantsByCell.TryGetValue(cell, out var plant))
            return plant.ownerPlayerIndex;

        return null;
    }

    /// <summary>Server-side validation for the prepare request.</summary>
    public bool CanPrepareServer(Vector3Int cell) =>
        tileStates.TryGetValue(cell, out var state) && state == TileState.NotPrepared;
    #endregion

    #region Actions: Prepare / Plant / Water (request entry points)
    public void PrepareTile(Vector3Int cell)
    {
        if (GameSession.OnlineActive)
        {
            NetworkPlayer.LocalPlayer?.RequestPrepareTile(cell);
            return;
        }

        if (!tileStates.TryGetValue(cell, out var state) || state != TileState.NotPrepared)
            return;

        ApplyPrepareTile(cell);
    }

    public void PlantSeed(Vector3Int cell, int playerIndex, GameObject plantPrefab)
    {
        // Online planting goes through Seed.Use -> NetworkPlayer.RequestPlantSeed.
        if (GameSession.OnlineActive) return;

        if (!tileStates.ContainsKey(cell) || !IsPrepared(cell) || IsOccupied(cell))
            return;

        ApplySeedTile(cell);
        SpawnPlantLocal(cell, playerIndex, plantPrefab);
    }

    public bool TryIrrigatePlant(Vector3Int cell)
    {
        if (GameSession.OnlineActive)
        {
            if (!HasPlant(cell)) return false;
            NetworkPlayer.LocalPlayer?.RequestIrrigate(cell);
            return true;
        }

        if (plantsByCell.TryGetValue(cell, out var plant))
        {
            plant.WaterPlant();
            return true;
        }
        return false;
    }

    public bool TryFertilizePlant(Vector3Int cell)
    {
        if (GameSession.OnlineActive)
        {
            if (!HasPlant(cell)) return false;
            NetworkPlayer.LocalPlayer?.RequestFertilize(cell);
            return true;
        }

        if (plantsByCell.TryGetValue(cell, out var plant))
        {
            plant.FertilizePlant();
            return true;
        }
        return false;
    }
    #endregion

    #region Server-side helpers (executed where the simulation runs)
    public void ServerIrrigate(Vector3Int cell)
    {
        if (plantsByCell.TryGetValue(cell, out var plant))
            plant.WaterPlant();
    }

    public void ServerFertilize(Vector3Int cell)
    {
        if (plantsByCell.TryGetValue(cell, out var plant))
            plant.FertilizePlant();
    }
    #endregion

    #region Mirrored state application (runs on every peer via ClientRpc)
    public void ApplyPrepareTile(Vector3Int cell)
    {
        farmTilemap.SetTile(cell, preparedTile);
        farmTilemap.RefreshTile(cell);
        tileStates[cell] = TileState.Prepared;
        NetworkMetrics.NoteActionApplied(cell); // telemetry: action latency end
    }

    public void ApplySeedTile(Vector3Int cell)
    {
        farmTilemap.SetTile(cell, seedTile);
        tileStates[cell] = TileState.PlantedSeed;
        NetworkMetrics.NoteActionApplied(cell);
    }

    public void ApplyClearTile(Vector3Int cell)
    {
        farmTilemap.SetTile(cell, preparedTile);
        tileStates[cell] = TileState.Prepared;
    }

    /// <summary>Called by networked plants when they spawn on this peer.</summary>
    public void RegisterPlant(Plant plant)
    {
        if (plant == null) return;
        plantsByCell[plant.cellPos] = plant;
        occupiedCells.Add(plant.cellPos);
        tileStates[plant.cellPos] = TileState.PlantedSeed;
    }

    /// <summary>Called by networked plants when they despawn on this peer.</summary>
    public void UnregisterPlant(Plant plant)
    {
        if (plant == null) return;
        if (plantsByCell.TryGetValue(plant.cellPos, out var registered) && registered == plant)
        {
            plantsByCell.Remove(plant.cellPos);
            occupiedCells.Remove(plant.cellPos);
        }
    }
    #endregion

    #region Internal Spawn & Remove
    private void SpawnPlantLocal(Vector3Int cell, int playerIndex, GameObject prefab)
    {
        Vector3 worldPos = farmTilemap.GetCellCenterWorld(cell);
        GameObject plantObj = Instantiate(prefab, worldPos, Quaternion.identity);

        plantObj.transform.SetParent(GetPlayerPlantRoot(playerIndex));

        Plant plant = plantObj.GetComponent<Plant>();
        plant?.Init(playerIndex, cell);

        plantsByCell[cell] = plant;
        occupiedCells.Add(cell);
    }

    public void RemovePlant(Vector3Int cell)
    {
        if (GameSession.OnlineActive)
        {
            // Sabotage-style removal (scissors): the server allows cutting any plant.
            if (!HasPlant(cell)) return;
            NetworkPlayer.LocalPlayer?.RequestRemovePlant(cell, sabotage: true);
            return;
        }

        if (!plantsByCell.TryGetValue(cell, out var plant))
            return;

        Destroy(plant.gameObject);
        plantsByCell.Remove(cell);
        occupiedCells.Remove(cell);

        farmTilemap.SetTile(cell, preparedTile);
        tileStates[cell] = TileState.Prepared;
    }

    public void NotifyPlantDeath(Vector3Int cell)
    {
        if (!plantsByCell.ContainsKey(cell))
            return;

        plantsByCell.Remove(cell);
        occupiedCells.Remove(cell);
        farmTilemap.SetTile(cell, preparedTile);
        tileStates[cell] = TileState.Prepared;
    }

    public bool TryRemovePlant(Vector3Int cell, int requesterPlayerIndex)
    {
        if (GameSession.OnlineActive)
        {
            if (!HasPlant(cell)) return false;
            if (GetPlantOwner(cell) != requesterPlayerIndex) return false;
            NetworkPlayer.LocalPlayer?.RequestRemovePlant(cell, sabotage: false);
            return true;
        }

        if (!plantsByCell.TryGetValue(cell, out var plant))
            return false;

        if (plant.ownerPlayerIndex != requesterPlayerIndex)
        {
            Debug.Log("Can't remove someone else's plant.");
            return false;
        }

        RemovePlant(cell);
        return true;
    }

    public Plant TryGetPlant(Vector3Int cell)
    {
        plantsByCell.TryGetValue(cell, out var plant);
        return plant;
    }
    #endregion

    #region Player Plant Root
    private Transform GetPlayerPlantRoot(int playerIndex)
    {
        if (!playerPlantRoots.TryGetValue(playerIndex, out var root) || root == null)
        {
            GameObject go = new($"Player{playerIndex}_Plants");
            go.transform.SetParent(plantsRoot, false);
            root = go.transform;
            playerPlantRoots[playerIndex] = root;
        }
        return root;
    }
    #endregion

    #region Water Tile
    public bool IsWaterTile(Vector3Int cell) => waterTilemap.HasTile(cell);
    #endregion

}
