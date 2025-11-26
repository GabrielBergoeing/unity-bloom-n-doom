#if UNITY_EDITOR
using System.Linq;
using UnityEngine;

public class LevelEditorRuntime : MonoBehaviour
{
    [Header("References")]
    public LevelManager levelManager;
    public Camera sceneCamera;

    [Header("Placement Settings")]
    public LevelObjectType currentType = LevelObjectType.Wall;
    public string currentSubtype = "North";
    public bool editingEnabled = true; // toggleable during runtime

    private void Awake()
    {
        if (sceneCamera == null)
            sceneCamera = Camera.main;

        Debug.Log("<color=yellow>[Editor]</color> Runtime level editor active.");
    }

    private void Update()
    {
        // Toggle edit mode
        if (Input.GetKeyDown(KeyCode.E))
        {
            editingEnabled = !editingEnabled;
            Debug.Log("Editing mode: " + editingEnabled);
        }

        if (!editingEnabled) return;
        HandleMouseInput();
    }

    private void HandleMouseInput()
    {
        if (Input.GetMouseButtonDown(0))     // Left click = place
            ProcessEditorClick(place: true);

        if (Input.GetMouseButtonDown(1))     // Right click = delete
            ProcessEditorClick(place: false);
    }

    private void ProcessEditorClick(bool place)
    {
        if (sceneCamera == null) return;

        Ray ray = sceneCamera.ScreenPointToRay(Input.mousePosition);
        Plane plane = new Plane(Vector3.forward, Vector3.zero);

        if (plane.Raycast(ray, out float distance))
        {
            Vector3 worldPos = ray.GetPoint(distance);
            Vector3Int gridPos = Vector3Int.FloorToInt(worldPos);

            if (place) 
                Place(gridPos);
            else 
                Delete(gridPos);
        }
    }

    // -------------------------------
    // PLACEMENT LOGIC
    // -------------------------------
    private void Place(Vector3Int gridPos)
    {
        GridData data = levelManager.GetLoadedLevel() ?? new GridData();

        if (data.objects.Any(o => o.x == gridPos.x && o.y == gridPos.y))
        {
            Debug.Log($"[Editor] Skipped duplicate at {gridPos}");
            return;
        }

        // Spawn on tilemap / scene
        levelManager.factory.Create(currentType, gridPos, currentSubtype);

        // Save level object
        data.objects.Add(new GridObjectData
        {
            type = currentType.ToString(),
            subtype = currentSubtype,
            x = gridPos.x,
            y = gridPos.y
        });

        levelManager.SaveLevel(data);
        Debug.Log($"[Editor] Placed {currentType} ({currentSubtype}) at {gridPos}");
    }

    // -------------------------------
    // DELETE LOGIC
    // -------------------------------
    private void Delete(Vector3Int gridPos)
    {
        GridData data = levelManager.GetLoadedLevel();
        if (data == null || data.objects.Count == 0)
        {
            Debug.LogWarning("[Editor] Nothing stored to delete");
            return;
        }

        var obj = data.objects.FirstOrDefault(o => o.x == gridPos.x && o.y == gridPos.y);
        if (obj == null)
        {
            Debug.Log($"[Editor] No object found at {gridPos}");
            return;
        }

        // Remove tile (for Walls, Land, Water)
        RemoveTile(gridPos);

        // Remove scene prefab if any (for non-tile objects)
        RemovePrefab(gridPos);

        // Remove from level data & save
        data.objects.Remove(obj);
        levelManager.SaveLevel(data);

        Debug.Log($"[Editor] Deleted {obj.type} at {gridPos}");
    }

    private void RemoveTile(Vector3Int gridPos)
    {
        // Let the universal handler clear tiles
        foreach (var handler in levelManager.factory.GetComponentsInChildren<TilePlacementHandler>())
            handler.ClearTile(gridPos);
    }

    private void RemovePrefab(Vector3Int gridPos)
    {
        Collider2D hit = Physics2D.OverlapPoint(new Vector2(gridPos.x, gridPos.y));
        if (hit != null)
            Destroy(hit.gameObject);
    }
}
#endif
