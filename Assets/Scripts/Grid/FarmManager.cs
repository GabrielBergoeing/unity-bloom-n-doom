using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class FarmManager : MonoBehaviour
{
    public static FarmManager instance;

    public Tilemap farmTilemap;
    public Tile preparedTile;
    public Tile seedTile;
    public GameObject plantPrefab;
    [SerializeField] private Transform plantsRoot;
    private readonly Dictionary<Vector3Int, TileState> tileStates = new();
    private readonly HashSet<Vector3Int> occupiedCells = new();
    private readonly Dictionary<int, Transform> playerPlantRoots = new();
    private readonly Dictionary<Vector3Int, Plant> plantsByCell = new();

    public enum TileState { NotPrepared, Prepared, PlantedSeed }

    void Awake() => instance = this;

    void Start()
    {
        BoundsInt bounds = farmTilemap.cellBounds;
        foreach (var pos in bounds.allPositionsWithin)
            if (farmTilemap.HasTile(pos))
                tileStates[pos] = TileState.NotPrepared;

        if (plantsRoot == null)
            plantsRoot = new GameObject("Plants").transform;
    }

    public void PrepareTile(Vector3Int cellPos)
    {
        if (tileStates.TryGetValue(cellPos, out var state) && state == TileState.NotPrepared)
        {
            farmTilemap.SetTile(cellPos, preparedTile);
            tileStates[cellPos] = TileState.Prepared;
        }
    }

    public void PlantSeed(Vector3Int cellPos, int planterPlayerIndex)
    {
        if (!tileStates.ContainsKey(cellPos)) return;
        if (IsOccupied(cellPos)) return;

        if (tileStates[cellPos] == TileState.Prepared)
        {
            farmTilemap.SetTile(cellPos, seedTile);
            tileStates[cellPos] = TileState.PlantedSeed;

            Vector3 worldPos = farmTilemap.GetCellCenterWorld(cellPos);
            if (plantPrefab != null)
            {
                var go = Instantiate(plantPrefab, worldPos, Quaternion.identity);

                // parent por jugador
                Transform root = GetOrCreatePlayerRoot(planterPlayerIndex);
                go.transform.SetParent(root, true);

                occupiedCells.Add(cellPos);

                var plant = go.GetComponent<Plant>();
                if (plant != null)
                {
                    plant.Init(planterPlayerIndex, cellPos);
                    plantsByCell[cellPos] = plant;
                }
            }
        }
    }


    public bool TryInteractPlant(Vector3Int cellPos)
    {
        if (plantsByCell.TryGetValue(cellPos, out var plant) && plant != null)
        {
            plant.Interact(); // suma “riegos”
            return true;
        }
        return false;
    }

    private Transform GetOrCreatePlayerRoot(int playerIndex)
    {
        if (!playerPlantRoots.TryGetValue(playerIndex, out var root) || root == null)
        {
            var go = new GameObject($"Player{playerIndex}_Plants");
            go.transform.SetParent(plantsRoot, false);
            root = go.transform;
            playerPlantRoots[playerIndex] = root;
        }
        return root;
    }

    public bool IsPrepared(Vector3Int cellPos) =>
        tileStates.ContainsKey(cellPos) && tileStates[cellPos] == TileState.Prepared;

    public bool IsOccupied(Vector3Int cellPos) => occupiedCells.Contains(cellPos);

    public void RemovePlant(Vector3Int cellPos)
    {
        if (!occupiedCells.Contains(cellPos))
            return;

        foreach (Transform root in plantsRoot)
        {
            foreach (Transform plant in root)
            {
                Vector3Int plantCell = farmTilemap.WorldToCell(plant.position);
                if (plantCell == cellPos)
                {
                    Destroy(plant.gameObject);
                    occupiedCells.Remove(cellPos);

                    if (tileStates.ContainsKey(cellPos))
                    {
                        tileStates[cellPos] = TileState.Prepared;
                        farmTilemap.SetTile(cellPos, preparedTile);
                    }

                    Debug.Log($"Planta en {cellPos} cortada y eliminada correctamente.");
                    return;
                }
            }
        }
    }

    public bool TryRemovePlant(Vector3Int cellPos, int requesterPlayerIndex)
    {
        if (plantsByCell.TryGetValue(cellPos, out var plant) && plant != null)
        {
            // Regla: solo el dueño puede eliminar su planta
            if (plant.ownerPlayerIndex != requesterPlayerIndex)
            {
                Debug.Log("⛔ No puedes eliminar plantas de otro jugador.");
                return false;
            }

            // Destruir el GameObject
            Destroy(plant.gameObject);

            // Limpiar estado de celda
            plantsByCell.Remove(cellPos);
            occupiedCells.Remove(cellPos);

            // Opcional: dejar el tile en 'Prepared' para replantar
            farmTilemap.SetTile(cellPos, preparedTile);
            tileStates[cellPos] = TileState.Prepared;

            return true;
        }

        Debug.Log("No hay ninguna planta en esta celda.");
        return false;
    }
}
