using UnityEngine;
using UnityEngine.InputSystem;

public class TileInteraction : MonoBehaviour
{
    [Header("References")]
    public Camera cam;
    public FarmManager farmManager;
    public Player player;

    [Header("Visuals")]
    public GameObject tileOutlinePrefab;
    private GameObject currentOutline;

    private PlayerInput playerInput;
    private InputAction interactAction;
    private Vector3Int currentCell;

    void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
    }

    void Start()
    {
        if (cam == null)
            cam = Camera.main;

        if (playerInput == null)
        {
            Debug.LogError("PlayerInput no asignado en TileInteraction.");
            return;
        }

        var map = playerInput.currentActionMap;
        if (map == null)
        {
            Debug.LogError("No hay Action Map activo en PlayerInput.");
            return;
        }

        interactAction = map.FindAction("Interact", false);
        if (interactAction == null)
        {
            Debug.LogError("No se encontró la acción 'Interact' en el mapa actual.");
            return;
        }

        interactAction.performed += OnInteract;

        Debug.Log("Acción 'Interact' conectada correctamente. Control scheme: " + playerInput.currentControlScheme);
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
