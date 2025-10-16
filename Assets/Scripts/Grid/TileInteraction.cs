using UnityEngine;

public class TileInteraction : MonoBehaviour
{
    public Camera cam;
    public FarmManager farmManager;
    public Player player;

    void Update()
    {
        if (Input.GetMouseButtonDown(0)) // clic izquierdo
        {
            Vector3 mouseWorld = cam.ScreenToWorldPoint(Input.mousePosition);
            Vector3Int cellPos = farmManager.farmTilemap.WorldToCell(mouseWorld);

            // Aquí decides qué acción hacer, por ejemplo:
            farmManager.PrepareTile(cellPos);
        }

        if (Input.GetMouseButtonDown(1)) // clic derecho
        {
            Vector3 mouseWorld = cam.ScreenToWorldPoint(Input.mousePosition);
            Vector3Int cellPos = farmManager.farmTilemap.WorldToCell(mouseWorld);

            farmManager.PlantSeed(cellPos);
        }
    }

    public void SetCamera(Camera newCam)
    {
        cam = newCam;
        Debug.Log("Camera asigned to TileInteraction: " + cam.name);
    }
}
