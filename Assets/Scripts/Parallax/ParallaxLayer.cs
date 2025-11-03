using UnityEngine;

[System.Serializable]
public class ParallaxLayer
{
    [SerializeField] private Transform background;
    [SerializeField] private float parallaxMultiplier;
    private float imageFullWidth;
    private float imageHalfWidth;

    public void CalculateImageWidth()
    {
        imageFullWidth = background.GetComponent<SpriteRenderer>().bounds.size.x;
        imageHalfWidth = imageFullWidth / 2;
    }
    public void Move(float distanceToMoveX, float distanceToMoveY)
    {
        background.position += new Vector3(distanceToMoveX, distanceToMoveY) * parallaxMultiplier;
    }

    public void LoopBackground(float cameraLeftEdge, float cameraRightEdge)
    {
        float spriteLeft = background.position.x - imageHalfWidth;
        float spriteRight = background.position.x + imageHalfWidth;

        if (spriteRight < cameraLeftEdge)
            background.position += Vector3.right * (imageFullWidth * 2);

        else if (spriteLeft > cameraRightEdge)
            background.position -= Vector3.right * (imageFullWidth * 2);
    }
}
