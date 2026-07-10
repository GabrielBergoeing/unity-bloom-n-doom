using UnityEngine;
using UnityEngine.UI;

// Small world-space progress bar shown above the player while an action
// (irrigate/plant/prepare ground/remove/sabotage) is locking their control via
// Player_ActionState.ExecuteAction. Built at runtime so no prefab needs editing.
public class Player_ActionCooldownVisual : MonoBehaviour
{
    [SerializeField] private float heightOffset = 1.2f;
    [SerializeField] private Vector2 barSize = new Vector2(0.6f, 0.09f);
    [SerializeField] private Color fillColor = new Color(0.35f, 0.85f, 1f);
    [SerializeField] private Color backgroundColor = new Color(0f, 0f, 0f, 0.5f);

    private GameObject canvasGO;
    private Image fillImage;

    private void Awake()
    {
        BuildVisual();
        SetVisible(false);
    }

    private void BuildVisual()
    {
        canvasGO = new GameObject("ActionCooldownCanvas");
        canvasGO.transform.SetParent(transform, false);
        canvasGO.transform.localPosition = new Vector3(0f, heightOffset, 0f);
        canvasGO.transform.localRotation = Quaternion.identity;

        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 10;

        RectTransform canvasRect = canvasGO.GetComponent<RectTransform>();
        canvasRect.sizeDelta = barSize;
        canvasRect.localScale = Vector3.one;

        GameObject bgGO = new GameObject("Background");
        bgGO.transform.SetParent(canvasGO.transform, false);
        Image bg = bgGO.AddComponent<Image>();
        bg.color = backgroundColor;
        RectTransform bgRect = bg.rectTransform;
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        GameObject fillGO = new GameObject("Fill");
        fillGO.transform.SetParent(canvasGO.transform, false);
        fillImage = fillGO.AddComponent<Image>();
        fillImage.color = fillColor;
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        fillImage.fillAmount = 0f;
        RectTransform fillRect = fillImage.rectTransform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
    }

    public void SetProgress(float t)
    {
        if (fillImage != null)
            fillImage.fillAmount = Mathf.Clamp01(t);
    }

    public void SetVisible(bool visible)
    {
        if (canvasGO != null)
            canvasGO.SetActive(visible);
    }
}
