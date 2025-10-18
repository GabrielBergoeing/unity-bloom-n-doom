using UnityEngine;

public class Plant : MonoBehaviour
{
    public SpriteRenderer spriteRenderer;

    public void Init(Sprite sprite)
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (sprite != null)
            spriteRenderer.sprite = sprite;
    }
    private void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
    }
}
