using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class Plant_VFX : MonoBehaviour
{
    private Plant plant;
    private SpriteRenderer spriteRenderer;

    [Header("Darkening Effect")]
    [SerializeField, Range(0f, 1f)] private float minBrightness = 0.2f;
    private const float maxBrightness = 1f;

    [Header("Player Border")]
    [SerializeField] private SpriteRenderer borderRenderer;
    [SerializeField] private Color[] playerColors;
    [SerializeField] private float borderScale = 1.5f;

    private void Awake()
    {
        plant = GetComponent<Plant>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        // Find or create border child
        if (!borderRenderer)
            FindBorder();

        // Default color palette
        if (playerColors == null || playerColors.Length == 0)
        {
            playerColors = new Color[]
            {
                Color.red,
                Color.blue,
                Color.green,
                Color.yellow
            };
        }
    }

    private void Update()
    {
        if (plant == null) return;

        UpdateDarkening();
        UpdateBorderColor();
        SyncBorderSprite();
    }

    private void UpdateDarkening()
    {
        if (plant.isOnFire) return;
        
        float witherRatio = Mathf.Clamp01(plant.GetWitherRatio());
        float brightness = Mathf.Lerp(minBrightness, maxBrightness, witherRatio);

        spriteRenderer.color = new Color(brightness, brightness, brightness, 1f);
    }

    private void UpdateBorderColor()
    {
        if (!borderRenderer || plant.ownerPlayerIndex < 0) return;

        int index = Mathf.Clamp(plant.ownerPlayerIndex, 0, playerColors.Length - 1);
        Color playerColor = playerColors[index];
        borderRenderer.color = playerColor * 1.3f; // Slightly brightened
    }

    private void SyncBorderSprite()
    {
        if (borderRenderer == null || spriteRenderer == null) return;

        // Match sprite and sorting layer
        borderRenderer.sprite = spriteRenderer.sprite;
        borderRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
        borderRenderer.sortingOrder = spriteRenderer.sortingOrder - 1;
    }

    private void FindBorder()
    {
        Transform child = transform.Find("Border");
        if (child)
            borderRenderer = child.GetComponent<SpriteRenderer>();
        else
            InitBorder();
    }

    private void InitBorder()
    {
        GameObject borderObj = new GameObject("Border");
        borderObj.transform.SetParent(transform);
        borderObj.transform.localPosition = Vector3.zero;
        borderObj.transform.localScale = Vector3.one * borderScale;

        borderRenderer = borderObj.AddComponent<SpriteRenderer>();
        borderRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
        borderRenderer.sortingOrder = spriteRenderer.sortingOrder - 1; // Behind main sprite
        borderRenderer.color = Color.white;
    }
}
