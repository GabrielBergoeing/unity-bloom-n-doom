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
    private Vector3Int currentCell;
    public Vector3Int CurrentCell => currentCell;

    private void Awake()
    {
        input = player.input;
        farmManager = FarmManager.instance;
    }

    private void Start()
    {
        if (cam == null) cam = Camera.main;
    }

    private void Update()
    {
        if (!player.canControl)
            return;
        
        Vector3 playerWorldPos = player.transform.position;
        Vector3Int playerCell = farmManager.farmTilemap.WorldToCell(playerWorldPos);
        Vector3Int frontCell = GetCellInFrontOfPlayer(playerCell);
        currentCell = frontCell;
        Vector3 cellCenter = farmManager.farmTilemap.GetCellCenterWorld(currentCell);
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
    public bool CellIsPrepared() => farmManager.IsPrepared(currentCell);
    public bool CellIsOccupied() => farmManager.IsOccupied(currentCell);
    public bool IsCellOwner(int playerIndex) => playerIndex == farmManager.GetPlantOwner(currentCell);

    public bool CanPrepare() => !CellIsPrepared() && !CellIsOccupied();
    public bool CanPlant() => CellIsPrepared() && !CellIsOccupied();
    public bool CanIrrigate() => CellIsOccupied();
    public bool CanRemove() => CellIsOccupied() && IsCellOwner(input.playerIndex);
    public bool CanSabotage() => CellIsOccupied() && !IsCellOwner(input.playerIndex);

    public void IrrigateInCell() => farmManager.TryIrrigatePlant(currentCell);
    public void RemoveInCell() => farmManager.TryRemovePlant(currentCell, input.playerIndex);
}
