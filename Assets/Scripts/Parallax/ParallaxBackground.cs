using UnityEngine;

public class ParallaxBackground : MonoBehaviour
{
    private Camera mainCamara;
    private float lastCameraPositionX;
    private float lastCameraPositionY;
    private float cameraHalfWidth;
    private float cameraHalfHeight;

    [SerializeField] private ParallaxLayer[] backgroundLayers;
    [SerializeField] private float autoScrollSpeed = 1f;


    private void Awake()
    {
        mainCamara = Camera.main;
        lastCameraPositionX = mainCamara.transform.position.x;
        lastCameraPositionY = mainCamara.transform.position.y;

        cameraHalfWidth = mainCamara.orthographicSize * mainCamara.aspect;
        cameraHalfHeight = mainCamara.orthographicSize;
        CalculateImageLength();
    }

    private void FixedUpdate()
    {
        float cameraPositionX = mainCamara.transform.position.x;
        float cameraPositionY = mainCamara.transform.position.y;

        // ✅ Auto-scroll background (simulate camera moving right)
        cameraPositionX += autoScrollSpeed * Time.fixedDeltaTime;
        mainCamara.transform.position = new Vector3(cameraPositionX, cameraPositionY, mainCamara.transform.position.z);

        float distanceToMoveX = cameraPositionX - lastCameraPositionX;
        float distanceToMoveY = cameraPositionY - lastCameraPositionY;

        lastCameraPositionX = cameraPositionX;
        lastCameraPositionY = cameraPositionY;

        float cameraLeftEdge = cameraPositionX - cameraHalfWidth;
        float cameraRightEdge = cameraPositionX + cameraHalfWidth;

        foreach (ParallaxLayer layer in backgroundLayers)
        {
            layer.Move(distanceToMoveX, distanceToMoveY);
            layer.LoopBackground(cameraLeftEdge, cameraRightEdge);
        }
    }

    private void CalculateImageLength()
    {
        foreach (ParallaxLayer layer in backgroundLayers)
            layer.CalculateImageWidth();
    }
}
