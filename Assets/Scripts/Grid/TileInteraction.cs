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
        // No dependemos de player.input aquí porque Player.Awake() podría no haber corrido aún.
    }

    private void Start()
    {
        if (cam == null) cam = Camera.main;

        farmManager = FarmManager.instance;

        // Intentar asignar input de forma segura (podría no estar listo en Awake)
        if (player != null)
            input = player.input ?? GetComponentInParent<Player>()?.input;
    }

    private void Update()
    {
        if (player == null) return;

        if (!player.canControl || !player.isLocalPlayer)
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
