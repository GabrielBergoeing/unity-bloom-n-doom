using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using Mirror;

[RequireComponent(typeof(NetworkIdentity))]
public class FarmManager : NetworkBehaviour
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

    // Eliminar/ignorar la asignaci�n en Awake; usar OnStartServer/OnStartClient
    private void Awake()
    {
        // no establecer 'instance' aqu� para evitar referenciar instancias no networked
        if (!startFarm)
            LevelManager.OnLevelLoaded += () => HandleLevelLoaded();
        else
            InitializeTileStates(true);
    }

    private void Start()
    {
        EnsurePlantsRootExists();
    }

    // Nuevo: confirmaci�n y fallback cuando el objeto se inicializa en el cliente
    public override void OnStartClient()
    {
        base.OnStartClient();

        // On a pure (non-host) client, OnStartServer never runs, so `instance` was never
        // assigned — every client-side reader (TileInteraction, etc.) saw it as null.
        if (instance == null)
            instance = this;

        Debug.Log($"[FarmManager][OnStartClient] farmTilemap={(farmTilemap==null?"NULL":"OK")} preparedTile={(preparedTile==null?"NULL":"OK")} seedTile={(seedTile==null?"NULL":"OK")}");
        if (farmTilemap == null)
        {
            // intento de fallback: tomar el primer Tilemap de la escena (�til en debugging)
            var any = FindObjectOfType<Tilemap>();
            if (any != null)
            {
                farmTilemap = any;
                Debug.Log($"[FarmManager] Assigned farmTilemap fallback -> {any.name}");
            }
            else
            {
                Debug.LogWarning("[FarmManager] No Tilemap found in scene during OnStartClient.");
            }
        }
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        instance = this;
        Debug.Log($"[FarmManager][OnStartServer] assigned instance on server (netId={netId})");
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        // si esta instancia cliente era la instance global, limpiarla
        if (instance == this)
            instance = null;
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
        if (instance == this)
            instance = null;
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
    // Marcar PrepareTile como [Server] y loggear ejecuci�n
    [Server]
    public void PrepareTile(Vector3Int cell)
    {
        Debug.Log($"[FarmManager][PrepareTile] Server preparing tile at {cell} (farmTilemap={(farmTilemap==null?"NULL":"OK")})");
        if (farmTilemap == null) return;

        if (!farmTilemap.HasTile(cell) || farmTilemap.GetTile(cell) != preparedTile)
        {
            farmTilemap.SetTile(cell, preparedTile);
            farmTilemap.RefreshTile(cell);
            RpcSetTileState(cell.x, cell.y, cell.z, (byte)TileState.Prepared);
        }
    }

    // Server-side spawn plant
    [Server]
    public void PlantSeed(Vector3Int cell, int playerIndex, GameObject plantPrefab)
    {
        Debug.Log($"[FarmManager][Server] PlantSeed request cell={cell} playerIndex={playerIndex} prefab={(plantPrefab!=null?plantPrefab.name:"NULL")}");
        if (!farmTilemap.HasTile(cell) || IsOccupied(cell))
        {
            Debug.Log($"[FarmManager] PlantSeed aborted: no tile or occupied. HasTile={farmTilemap.HasTile(cell)} IsOccupied={IsOccupied(cell)}");
            return;
        }

        // mark tile visually (server)
        farmTilemap.SetTile(cell, seedTile);

        // Propagar tile change a clientes
        RpcSetTileState(cell.x, cell.y, cell.z, (byte)TileState.PlantedSeed);

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
            // propagar a clientes
            RpcSetTileState(cell.x, cell.y, cell.z, (byte)TileState.Prepared);
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
        {
            farmTilemap.SetTile(cell, preparedTile);
            RpcSetTileState(cell.x, cell.y, cell.z, (byte)TileState.Prepared);
        }
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

    #region RPCs
    // Aseg�rate de que RpcSetTileState tambi�n loggea su llegada
    [ClientRpc]
    private void RpcSetTileState(int x, int y, int z, byte state)
    {
        Debug.Log($"[FarmManager][RpcSetTileState] Received state={state} on client. farmTilemap={(farmTilemap==null?"NULL":"OK")}");
        if (farmTilemap == null) return;
        var cell = new Vector3Int(x, y, z);
        Tile tileToSet = null;
        if (state == (byte)TileState.Prepared)
            tileToSet = preparedTile;
        else if (state == (byte)TileState.PlantedSeed)
            tileToSet = seedTile;
        farmTilemap.SetTile(cell, tileToSet);
        farmTilemap.RefreshTile(cell);
    }
    #endregion

}
