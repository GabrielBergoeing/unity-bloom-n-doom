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
    private readonly Dictionary<Vector3Int, Plant> plantsByCell = new();
    private readonly HashSet<Vector3Int> occupiedCells = new();

    // --- Player Organisation ---
    private readonly Dictionary<int, Transform> playerPlantRoots = new();

    public enum TileState { NotPrepared, Prepared, PlantedSeed }

    private bool initialized = false;

    private void Awake()
    {
        // Asignación para modo offline (sin Mirror)
        bool isNetworkActive = NetworkServer.active || NetworkClient.active;
        if (!isNetworkActive)
        {
            instance = this;
            Debug.Log("[FarmManager] Modo offline. Asignando instance.");
        }

        if (!startFarm)
        {
            LevelManager.OnLevelLoaded += () => HandleLevelLoaded();
            Debug.Log("[FarmManager] Registrado en LevelManager.OnLevelLoaded");
        }
        else
        {
            InitializeTileStates(true);
        }
    }

    private void Start()
    {
        EnsurePlantsRootExists();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        instance = this;
        Debug.Log($"[FarmManager][OnStartServer] assigned instance on server (netId={netId})");
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        // En cliente puro, OnStartServer nunca corre
        if (instance == null)
        {
            instance = this;
            Debug.Log($"[FarmManager][OnStartClient] assigned instance on client (netId={netId})");
        }

        // Validar referencias
        Debug.Log($"[FarmManager][OnStartClient] farmTilemap={(farmTilemap==null?"NULL":"OK")} preparedTile={(preparedTile==null?"NULL":"OK")} seedTile={(seedTile==null?"NULL":"OK")}");
        
        if (farmTilemap == null)
        {
            var any = FindObjectOfType<Tilemap>();
            if (any != null)
            {
                farmTilemap = any;
                Debug.Log($"[FarmManager] Asignado farmTilemap fallback -> {any.name}");
            }
            else
            {
                Debug.LogWarning("[FarmManager] No Tilemap encontrado en la escena durante OnStartClient.");
            }
        }
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        if (instance == this)
            instance = null;
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
        if (instance == this)
            instance = null;
    }

    private void HandleLevelLoaded()
    {
        if (initialized)
            return;

        initialized = true;
        Debug.Log("[FarmManager] LevelManager.OnLevelLoaded invocado.");
        InitializeTileStates(false);
    }

    private void InitializeTileStates(bool generateFarm)
    {
        if (generateFarm)
        {
            Debug.Log("[FarmManager] Generando farm automático.");
        }
        else
        {
            Debug.Log("[FarmManager] Inicializando estados de tiles.");
            // Aquí puedes sincronizar los estados desde el archivo de datos si es necesario
        }
    }

    private void EnsurePlantsRootExists()
    {
        if (plantsRoot == null)
        {
            plantsRoot = new GameObject("PlantsRoot").transform;
            plantsRoot.SetParent(transform);
            Debug.Log("[FarmManager] Creado PlantsRoot.");
        }
    }

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
        // En modo local (sin Mirror activo) o en servidor, permitir ejecución
        bool isNetworkActive = NetworkServer.active || NetworkClient.active;
        if (isNetworkActive && !isServer) return;

        Debug.Log($"[FarmManager][PrepareTile] Server preparing tile at {cell} (farmTilemap={(farmTilemap==null?"NULL":"OK")})");
        if (farmTilemap == null) return;

        if (!farmTilemap.HasTile(cell) || farmTilemap.GetTile(cell) != preparedTile)
        {
            farmTilemap.SetTile(cell, preparedTile);
            farmTilemap.RefreshTile(cell);
            RpcSetTileState(cell.x, cell.y, cell.z, (byte)TileState.Prepared);
        }
    }

    public void PlantSeed(Vector3Int cell, int playerIndex, GameObject plantPrefab)
    {
        // En modo local (sin Mirror activo) o en servidor, permitir ejecución
        bool isNetworkActive = NetworkServer.active || NetworkClient.active;
        if (isNetworkActive && !isServer) return;

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

    public bool TryIrrigatePlant(Vector3Int cell)
    {
        // En modo local (sin Mirror activo) o en servidor, permitir ejecución
        bool isNetworkActive = NetworkServer.active || NetworkClient.active;
        if (isNetworkActive && !isServer) return false;

        if (plantsByCell.TryGetValue(cell, out var plant))
        {
            plant.WaterPlant();
            return true;
        }
        return false;
    }

    public bool TryFertilizePlant(Vector3Int cell)
    {
        // En modo local (sin Mirror activo) o en servidor, permitir ejecución
        bool isNetworkActive = NetworkServer.active || NetworkClient.active;
        if (isNetworkActive && !isServer) return false;

        if (plantsByCell.TryGetValue(cell, out var plant))
        {
            plant.FertilizePlant();
            return true;
        }
        return false;
    }
    #endregion

    #region Internal Spawn & Remove

    private void SpawnPlant(Vector3Int cell, int playerIndex, GameObject prefab)
    {
        // En modo local (sin Mirror activo) o en servidor, permitir ejecución
        bool isNetworkActive = NetworkServer.active || NetworkClient.active;
        if (isNetworkActive && !isServer) return;

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

    public void RemovePlant(Vector3Int cell)
    {
        // En modo local (sin Mirror activo) o en servidor, permitir ejecución
        bool isNetworkActive = NetworkServer.active || NetworkClient.active;
        if (isNetworkActive && !isServer) return;

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
    private void RpcSetTileState(int x, int y, int z, byte state)
    {
        // En modo local (sin Mirror activo), ejecutar directamente
        // En modo online, solo ejecutar en cliente
        bool isNetworkActive = NetworkServer.active || NetworkClient.active;
        if (isNetworkActive && isServer)
        {
            // Si estamos en servidor online, enviar RPC a clientes
            if (isClient)
                RpcSetTileStateClient(x, y, z, state);
            return;
        }

        // Modo local: ejecutar directamente
        RpcSetTileStateClient(x, y, z, state);
    }

    [ClientRpc]
    private void RpcSetTileStateClient(int x, int y, int z, byte state)
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
