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

    private void Awake()
    {
        playerInput = GetComponentInParent<PlayerInput>();
        farmManager = FarmManager.instance;
    }

    private void Start()
    {
        if (cam == null) cam = Camera.main;

        interactAction = playerInput.actions["Interact"];
        if (interactAction == null)
        {
            Debug.LogError("No Interact action found for this player.");
            return;
        }

        interactAction.performed += OnInteract;

        Debug.Log($"Player {playerInput.playerIndex} bound to Interact. Control Scheme: {playerInput.currentControlScheme}");
    }



    void OnDestroy()
    {
        if (interactAction != null)
            interactAction.performed -= OnInteract;
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

        if (Input.GetMouseButtonDown(0))
            farmManager.PrepareTile(currentCell);
        if (Input.GetMouseButtonDown(1))
            farmManager.PlantSeed(currentCell);
    }

    private void OnInteract(InputAction.CallbackContext context)
    {
        if (!context.performed) return;

        if (farmManager.IsPrepared(currentCell))
            farmManager.PlantSeed(currentCell);
        else
            farmManager.PrepareTile(currentCell);
    }

    public void SetCamera(Camera newCam)
    {
        cam = newCam;
        Debug.Log("Camera assigned to TileInteraction: " + cam.name);
    }
}
