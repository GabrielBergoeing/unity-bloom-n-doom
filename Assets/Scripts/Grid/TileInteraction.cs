using UnityEngine;
using UnityEngine.InputSystem;

public class TileInteraction : MonoBehaviour
{
    [Header("References")]
    public Camera cam;
    public Player player;
    private FarmManager farmManager;

    [Header("Visuals")]
    public GameObject tileOutlinePrefab;
    private GameObject currentOutline;

    private PlayerInput input;
    
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

    private void Awake()
    {
        input = player.input;
    }

    private void Start()
    {
        if (cam == null) cam = Camera.main;
    }

    private void Update()
    {
        if (!player.canControl || !player.isLocalPlayer)
            return;
        
        if (FarmManager.instance == null || FarmManager.instance.farmTilemap == null) 
            return;

        Vector3Int cell = CurrentCell;
        Vector3 cellCenter = FarmManager.instance.farmTilemap.GetCellCenterWorld(cell);
        
        if (tileOutlinePrefab != null)
        {
            if (currentOutline == null)
                currentOutline = Instantiate(tileOutlinePrefab, cellCenter, Quaternion.identity);
            else
                currentOutline.transform.position = cellCenter;
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

    public bool CanPrepare() => !CellIsPrepared() && !CellIsOccupied();
    public bool CanPlant() => CellIsPrepared() && !CellIsOccupied();
    public bool CanIrrigate() => CellIsOccupied();
    public bool CanRemove() => CellIsOccupied() && IsCellOwner(input.playerIndex);
    public bool CanSabotage() => CellIsOccupied() && !IsCellOwner(input.playerIndex);
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
        int playerIndex = input != null ? input.playerIndex : -1;
        FarmManager.instance.TryRemovePlant(CurrentCell, playerIndex);
    }
}
