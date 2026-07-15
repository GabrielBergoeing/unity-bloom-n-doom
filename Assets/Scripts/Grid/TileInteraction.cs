using UnityEngine;

public class TileInteraction : MonoBehaviour
{
    [Header("References")]
    public Camera cam;
    public Player player;
    private FarmManager farmManager;

    [Header("Visuals")]
    public GameObject tileOutlinePrefab;
    private GameObject currentOutline;

    public Vector3Int CurrentCell
    {
        get
        {
            if (FarmManager.instance == null || FarmManager.instance.farmTilemap == null) 
                return Vector3Int.zero;
                
            Vector3 playerWorldPos = player.transform.position;
            Vector3Int playerCell = FarmManager.instance.farmTilemap.WorldToCell(playerWorldPos);
            return GetCellInFrontOfPlayer(playerCell);
        }
    }

    private void Start()
    {
        if (cam == null) cam = Camera.main;

        farmManager = FarmManager.instance;
    }

    private void Update()
    {
        if (player == null) return;

        bool isNetworkSpawnedObject = player.isServer || player.isClient;
        bool hasLocalControl = !isNetworkSpawnedObject || player.isLocalPlayer;

        if (!player.canControl || !hasLocalControl)
            return;
        
        if (farmManager == null || farmManager.farmTilemap == null) 
            return;

        Vector3Int cell = CurrentCell;
        Vector3 cellCenter = farmManager.farmTilemap.GetCellCenterWorld(cell);

        // Mantener el Z igual al del jugador para que el outline quede en la misma capa visual
        float z = player.transform.position.z;
        Vector3 targetPos = new Vector3(cellCenter.x, cellCenter.y, z);
        
        if (tileOutlinePrefab != null)
        {
            if (currentOutline == null)
            {
                // Hacer el outline hijo del Tilemap para respetar orden y transformaciones
                Transform parent = farmManager.farmTilemap.transform;
                currentOutline = Instantiate(tileOutlinePrefab, targetPos, Quaternion.identity, parent);
            }
            else
            {
                currentOutline.transform.position = targetPos;
            }
        }
    }

    private Vector3Int GetCellInFrontOfPlayer(Vector3Int playerCell)
    {
        Vector3Int offset = new Vector3Int(player.xFacingDir, player.yFacingDir, 0);
        
        if (offset == Vector3Int.zero)
            offset = Vector3Int.up;
        
        return playerCell + offset;
    }
    public void SetCamera(Camera newCam) => cam = newCam;

    // Interface functions so that player does not directly ask FarmManager
    public bool CellIsPrepared() => FarmManager.instance != null && FarmManager.instance.IsPrepared(CurrentCell);
    public bool CellIsOccupied() => FarmManager.instance != null && FarmManager.instance.IsOccupied(CurrentCell);
    public bool IsCellOwner(int playerIndex) => FarmManager.instance != null && playerIndex == FarmManager.instance.GetPlantOwner(CurrentCell);

    public bool CanPrepare() => !CellIsPrepared() && !CellIsOccupied()
        && FarmManager.instance != null
        && !FarmManager.instance.IsWaterTile(CurrentCell)
        && !FarmManager.instance.IsWallTile(CurrentCell)
        && !FarmManager.instance.IsConcreteTile(CurrentCell);
    public bool CanPlant() => CellIsPrepared() && !CellIsOccupied();
    public bool CanIrrigate() => CellIsOccupied() && player != null && IsCellOwner(player.OwnerIndex);
    public bool CanRemove() => CellIsOccupied() && player != null && IsCellOwner(player.OwnerIndex);
    public bool CanSabotage() => CellIsOccupied() && player != null && !IsCellOwner(player.OwnerIndex);
    public bool CanRefillWater() => FarmManager.instance != null && FarmManager.instance.IsWaterTile(CurrentCell);

    // These are called server-side from the state machine, so call FarmManager directly.
    public void IrrigateInCell()
    {
        if (FarmManager.instance == null) return;
        FarmManager.instance.TryIrrigatePlant(CurrentCell);
    }

    public void FertilizeInCell()
    {
        if (FarmManager.instance == null) return;
        FarmManager.instance.TryFertilizePlant(CurrentCell);
    }

    public void RemoveInCell()
    {
        if (FarmManager.instance == null) return;
        int playerIndex = player != null ? player.OwnerIndex : -1;
        FarmManager.instance.TryRemovePlant(CurrentCell, playerIndex);
    }
}
