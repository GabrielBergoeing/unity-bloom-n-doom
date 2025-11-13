using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class Plant_VFX : MonoBehaviour
{
    private Plant plant;
    private SpriteRenderer spriteRenderer;

    [Header("Darkening")]
    [SerializeField, Range(0f, 1f)] private float minBrightness = 0.2f;
    private const float maxBrightness = 1f;
    private float lastWitherRatio = -1f;

    [Header("Player Border")]
    [SerializeField] private SpriteRenderer borderRenderer;
    [SerializeField] private Color[] playerColors;
    [SerializeField] private float borderScale = 1.5f;

    private int lastOwnerIndex = -999;
    private Sprite lastSprite;

    private void Awake()
    {
        plant = GetComponent<Plant>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        if (!borderRenderer)
            FindOrCreateBorder();

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

        // Initial sync
        UpdateVisuals(force: true);
    }

    private void LateUpdate()
    {
        UpdateVisuals();
    }

    private void UpdateVisuals(bool force = false)
    {
        if (plant == null) return;

        UpdateDarkening(force);
        UpdateBorderColor(force);
        UpdateBorderSprite(force);
    }


    #region VFX
    private void UpdateDarkening(bool force)
    {
        if (plant.isOnFire) return;

        float ratio = Mathf.Clamp01(plant.GetWitherRatio());

        if (!force && Mathf.Abs(ratio - lastWitherRatio) < 0.001f)
            return;

        lastWitherRatio = ratio;

        float brightness = Mathf.Lerp(minBrightness, maxBrightness, ratio);
        spriteRenderer.color = new Color(brightness, brightness, brightness, 1f);
    }

    private void UpdateBorderColor(bool force)
    {
        if (!borderRenderer || plant.ownerPlayerIndex < 0) return;

        if (!force && plant.ownerPlayerIndex == lastOwnerIndex)
            return;

        lastOwnerIndex = plant.ownerPlayerIndex;

        int index = Mathf.Clamp(lastOwnerIndex, 0, playerColors.Length - 1);
        borderRenderer.color = playerColors[index] * 1.3f;
    }
    #endregion

    #region Updates & Init
    private void UpdateBorderSprite(bool force)
    {
        if (!borderRenderer || !spriteRenderer) return;

        if (!force && spriteRenderer.sprite == lastSprite)
            return;

        lastSprite = spriteRenderer.sprite;

        borderRenderer.sprite = lastSprite;
        borderRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
        borderRenderer.sortingOrder = spriteRenderer.sortingOrder - 1;
    }

    private void FindOrCreateBorder()
    {
        Transform child = transform.Find("Border");

        if (child)
        {
            borderRenderer = child.GetComponent<SpriteRenderer>();
            return;
        }

        GameObject obj = new GameObject("Border");
        obj.transform.SetParent(transform);
        obj.transform.localPosition = Vector3.zero;
        obj.transform.localScale = Vector3.one * borderScale;

        borderRenderer = obj.AddComponent<SpriteRenderer>();
        borderRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
        borderRenderer.sortingOrder = spriteRenderer.sortingOrder - 1;
        borderRenderer.color = Color.white;
    }
    #endregion
}
