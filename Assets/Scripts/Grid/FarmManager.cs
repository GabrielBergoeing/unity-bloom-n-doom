using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using Mirror;

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
    // NOTE: plantsByCell/occupiedCells are maintained on server and also maintained on client via Plant registration
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
            ; // keep other structures as clients will populate via Plant.OnStartClient
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
        // keep existing behaviour if needed; preparedTile logic not networked here
        farmTilemap != null && farmTilemap.HasTile(cell) && farmTilemap.GetTile(cell) == preparedTile;

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
    #endregion

    #region Actions: Prepare / Plant / Water
    public void PrepareTile(Vector3Int cell)
    {
        if (farmTilemap == null) return;

        if (!farmTilemap.HasTile(cell) || farmTilemap.GetTile(cell) != preparedTile)
        {
            farmTilemap.SetTile(cell, preparedTile);
            farmTilemap.RefreshTile(cell);
        }
    }

    // Server-side spawn plant
    [Server]
    public void PlantSeed(Vector3Int cell, int playerIndex, GameObject plantPrefab)
    {
        if (!farmTilemap.HasTile(cell) || IsOccupied(cell))
            return;

        // mark tile visually
        farmTilemap.SetTile(cell, seedTile);

        SpawnPlant(cell, playerIndex, plantPrefab);
    }

    // server-side wrapper used by gameplay logic - returns true if irrigated
    [Server]
    public bool TryIrrigatePlant(Vector3Int cell)
    {
        if (plantsByCell.TryGetValue(cell, out var plant))
        {
            plant.WaterPlant();
            return true;
        }
        return false;
    }

    [Server]
    public bool TryFertilizePlant(Vector3Int cell)
    {
        if (plantsByCell.TryGetValue(cell, out var plant))
        {
            plant.FertilizePlant();
            return true;
        }
        return false;
    }
    #endregion

    #region Internal Spawn & Remove

    [Server]
    private void SpawnPlant(Vector3Int cell, int playerIndex, GameObject prefab)
    {
        Vector3 worldPos = farmTilemap.GetCellCenterWorld(cell);

        // create parent root for server organization (optional)
        Transform parentRoot = GetPlayerPlantRoot(playerIndex);

        GameObject plantObj = Instantiate(prefab, worldPos, Quaternion.identity);
        plantObj.transform.SetParent(parentRoot, false);

        // spawn networked object
        NetworkServer.Spawn(plantObj);

        Plant plant = plantObj.GetComponent<Plant>();
        if (plant != null)
        {
            plant.InitServer(playerIndex, cell);
            plantsByCell[cell] = plant;
            occupiedCells.Add(cell);
        }
    }

    [Server]
    public void RemovePlant(Vector3Int cell)
    {
        if (!plantsByCell.TryGetValue(cell, out var plant))
            return;

        NetworkServer.Destroy(plant.gameObject);
        plantsByCell.Remove(cell);
        occupiedCells.Remove(cell);

        if (farmTilemap != null)
        {
            farmTilemap.SetTile(cell, preparedTile);
        }
    }

    [Server]
    public void NotifyPlantDeath(Vector3Int cell)
    {
        if (!plantsByCell.ContainsKey(cell))
            return;

        plantsByCell.Remove(cell);
        occupiedCells.Remove(cell);
        if (farmTilemap != null)
            farmTilemap.SetTile(cell, preparedTile);
    }

    [Server]
    public bool TryRemovePlant(Vector3Int cell, int requesterPlayerIndex)
    {
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

    #region Client registration (called by Plant.OnStartClient / OnStopClient)
    // Called on clients when a plant instance spawns locally
    public void RegisterClientPlant(Plant plant)
    {
        if (plant == null || farmTilemap == null) return;
        Vector3Int cell = farmTilemap.WorldToCell(plant.transform.position);
        plantsByCell[cell] = plant;
        occupiedCells.Add(cell);
    }

    // Called on clients when a plant instance is destroyed locally
    public void UnregisterClientPlant(Plant plant)
    {
        if (plant == null || farmTilemap == null) return;
        Vector3Int cell = farmTilemap.WorldToCell(plant.transform.position);
        plantsByCell.Remove(cell);
        occupiedCells.Remove(cell);
    }
    #endregion

    #region Water Tile
    public bool IsWaterTile(Vector3Int cell) => waterTilemap.HasTile(cell);
    #endregion

}
