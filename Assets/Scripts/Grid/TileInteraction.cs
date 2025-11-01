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

    private PlayerInput playerInput;
    private Vector3Int currentCell;
    private InputAction removeAction;

    public Vector3Int CurrentCell => currentCell;

    private void Awake()
    {
        playerInput = GetComponentInParent<PlayerInput>();
        farmManager = FarmManager.instance;
    }

    private void Start()
    {
        if (cam == null) cam = Camera.main;

        removeAction = playerInput.actions["Remove"];
        if (removeAction != null)
        {
            removeAction.performed += OnRemove;
        }
    }

    void OnDestroy()
    {
        if (removeAction != null)
            removeAction.performed -= OnRemove;
    }

    void Update()
    {
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

    private void OnRemove(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed) return;

        bool removed = farmManager.TryRemovePlant(currentCell, playerInput.playerIndex);
    }
    public void SetCamera(Camera newCam) => cam = newCam;
}
