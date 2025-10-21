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
    private InputAction interactAction;
    private Vector3Int currentCell;
    private InputAction removeAction;
    private InputAction irrigateAction;

    public Vector3Int CurrentCell => currentCell;

    public ParticleSystem irrigateVFX;

    private void Awake()
    {
        playerInput = GetComponentInParent<PlayerInput>();
        farmManager = FarmManager.instance;
    }

    private void Start()
    {
        if (cam == null) cam = Camera.main;

        /*
        interactAction = playerInput.actions["Interact"];
        if (interactAction == null)
        {
            Debug.LogError("No Interact action found for this player.");
            return;
        }

        */
        removeAction = playerInput.actions["Remove"];
        if (removeAction != null)
        {
            removeAction.performed += OnRemove;
        }
        irrigateAction = playerInput.actions["Irrigate"];
        if (irrigateAction != null)
        {
            irrigateAction.performed += OnIrrigate;
        }
        else
        {
            Debug.LogWarning("No 'Remove' action found. Usaré fallback con teclado X.");
        }

        //interactAction.performed += OnInteract;

        Debug.Log($"Player {playerInput.playerIndex} bound to Interact. Control Scheme: {playerInput.currentControlScheme}");
    }

    void OnDestroy()
    {
        //if (interactAction != null)
        //    interactAction.performed -= OnInteract;

        if (removeAction != null)
            removeAction.performed -= OnRemove;

        if (irrigateAction != null)
            irrigateAction.performed -= OnIrrigate;

    }

    void Update()
    {
        Vector3 mouseWorld = cam.ScreenToWorldPoint(Input.mousePosition);
        mouseWorld.z = 0;
        currentCell = farmManager.farmTilemap.WorldToCell(mouseWorld);
        Vector3 cellCenter = farmManager.farmTilemap.GetCellCenterWorld(currentCell);

        if (tileOutlinePrefab != null)
        {
            if (currentOutline == null)
                currentOutline = Instantiate(tileOutlinePrefab, cellCenter, Quaternion.identity);
            else
                currentOutline.transform.position = cellCenter;
        }
    }

    private void OnInteract(InputAction.CallbackContext context)
    {
        if (!context.performed) return;

        if (farmManager.TryInteractPlant(currentCell)) return;

        //if (farmManager.IsPrepared(currentCell))
        //{
        //    farmManager.PlantSeed(currentCell, playerInput.playerIndex);
        //    return;
        //}

        farmManager.PrepareTile(currentCell);
    }
    
    private void OnIrrigate(InputAction.CallbackContext contx)
    {
        if (!contx.performed || player.waterSupply < 10) return;
        // Debug.Log($"player pos: {player.transform.position}\ntile pos: {farmManager.farmTilemap.GetCellCenterLocal(currentCell)}");
        
        Vector3 playerPos = player.transform.position;
        Vector3 tilePos = farmManager.farmTilemap.GetCellCenterLocal(currentCell);
        Vector3 direction = tilePos - playerPos;

        float angleRad = Mathf.Atan2(direction.y, direction.x);
        float angleDeg = angleRad * Mathf.Rad2Deg;

        irrigateVFX.transform.rotation = Quaternion.Euler(0, 0, angleDeg);
        irrigateVFX.Play();

        player.waterSupply -= 10;
        Debug.Log($"waterSupply: {player.waterSupply}");

        farmManager.TryIrrigatePlant(currentCell);
    }

    private void OnRemove(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed) return;

        bool removed = farmManager.TryRemovePlant(currentCell, playerInput.playerIndex);
    }


    public void SetCamera(Camera newCam)
    {
        cam = newCam;
        Debug.Log("Camera assigned to TileInteraction: " + cam.name);
    }
}
