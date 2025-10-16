using UnityEngine;

public class TileInteraction : MonoBehaviour
{
    public Camera cam;
    public FarmManager farmManager;
    public Player player;

    public GameObject tileOutlinePrefab;
    private GameObject currentOutline;
    void Update()
    {
        Vector3 mouseWorld = cam.ScreenToWorldPoint(Input.mousePosition);
        mouseWorld.z = 0;
        Vector3Int cellPos = farmManager.farmTilemap.WorldToCell(mouseWorld);
        Vector3 cellCenter = farmManager.farmTilemap.GetCellCenterWorld(cellPos);

        if (currentOutline == null)
            currentOutline = Instantiate(tileOutlinePrefab, cellCenter, Quaternion.identity);
        else
            currentOutline.transform.position = cellCenter;

        if (Input.GetMouseButtonDown(0))
            farmManager.PrepareTile(cellPos);
        if (Input.GetMouseButtonDown(1))
            farmManager.PlantSeed(cellPos);
    }

    public void SetCamera(Camera newCam)
    {
        cam = newCam;
        Debug.Log("Camera asigned to TileInteraction: " + cam.name);
    }
}
