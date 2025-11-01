using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class FarmManager : MonoBehaviour
{
    public static FarmManager instance;

    [Header("References")]
    public Tilemap farmTilemap;
    public Tile preparedTile;
    public Tile seedTile;
    [SerializeField] private Transform plantsRoot;

    [Header("Plant Prefabs (index = seedType)")]
    public List<GameObject> plantPrefabs = new();

    // --- Tile State Tracking ---
    private readonly Dictionary<Vector3Int, TileState> tileStates = new();
    private readonly Dictionary<Vector3Int, Plant> plantsByCell = new();
    private readonly HashSet<Vector3Int> occupiedCells = new();

    // --- Player Organisation ---
    private readonly Dictionary<int, Transform> playerPlantRoots = new();

    public enum TileState { NotPrepared, Prepared, PlantedSeed }

    private void Awake() => instance = this;

    private void Start()
    {
        InitializeTileStates();
        EnsurePlantsRootExists();
    }

    #region Init Helpers
    private void InitializeTileStates()
    {
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
    #endregion

    #region Actions: Prepare / Plant / Water

    public void PrepareTile(Vector3Int cell)
    {
        if (!tileStates.TryGetValue(cell, out var state) || state != TileState.NotPrepared)
            return;

        farmTilemap.SetTile(cell, preparedTile);
        tileStates[cell] = TileState.Prepared;
    }

    public void PlantSeed(Vector3Int cell, int playerIndex, int seedType = 0)
    {
        if (!tileStates.ContainsKey(cell) || !IsPrepared(cell) || IsOccupied(cell))
            return;

        farmTilemap.SetTile(cell, seedTile);
        tileStates[cell] = TileState.PlantedSeed;

        CreatePlantInstance(cell, playerIndex, seedType);
    }

    public bool TryIrrigatePlant(Vector3Int cell)
    {
        if (plantsByCell.TryGetValue(cell, out var plant))
        {
            plant.WaterPlant();
            return true;
        }
        return false;
    }
    #endregion

    #region Internal Spawn & Remove
    private void CreatePlantInstance(Vector3Int cell, int playerIndex, int seedType)
    {
        Vector3 worldPos = farmTilemap.GetCellCenterWorld(cell);

        GameObject prefab = (seedType < plantPrefabs.Count) ? plantPrefabs[seedType] : null;
        if (prefab == null) return;

        GameObject plantObj = Instantiate(prefab, worldPos, Quaternion.identity);
        plantObj.transform.SetParent(GetPlayerPlantRoot(playerIndex));

        Plant plant = plantObj.GetComponent<Plant>();
        plant?.Init(playerIndex, cell);

        plantsByCell[cell] = plant;
        occupiedCells.Add(cell);
    }

    public void RemovePlant(Vector3Int cell)
    {
        if (!plantsByCell.TryGetValue(cell, out var plant))
            return;

        Destroy(plant.gameObject);
        plantsByCell.Remove(cell);
        occupiedCells.Remove(cell);

        farmTilemap.SetTile(cell, preparedTile);
        tileStates[cell] = TileState.Prepared;
    }

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
}
