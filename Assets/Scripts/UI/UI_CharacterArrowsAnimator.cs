using UnityEngine;
using UnityEngine.UI;

public class UI_CharacterArrowsAnimator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Image topArrow;
    [SerializeField] private Image bottomArrow;

    [Header("Animation Settings")]
    [SerializeField, Range(0.01f, 2f)] private float speed = 1f;
    [SerializeField, Range(5f, 50f)] private float moveDistance = 15f;

    private RectTransform topRect;
    private RectTransform bottomRect;

    private float baseTopY;
    private float baseBottomY;
    private bool isVisible = false;

    private void Awake()
    {
        topRect = topArrow.GetComponent<RectTransform>();
        bottomRect = bottomArrow.GetComponent<RectTransform>();

        baseTopY = topRect.anchoredPosition.y;
        baseBottomY = bottomRect.anchoredPosition.y;
        Hide();
    }

    private void Update()
    {
        if (!isVisible) return;

        float offset = Mathf.Sin(Time.time * speed) * moveDistance;

        topRect.anchoredPosition = new Vector2(topRect.anchoredPosition.x, baseTopY + offset);
        bottomRect.anchoredPosition = new Vector2(bottomRect.anchoredPosition.x, baseBottomY - offset);
    }

    // ========================================================
    // CALLED BY UI_CharacterSelector
    // ========================================================

    public void Show()
    {
        isVisible = true;
        topArrow.enabled = true;
        bottomArrow.enabled = true;
    }

    public void Hide()
    {
        isVisible = false;
        topArrow.enabled = false;
        bottomArrow.enabled = false;
    }
}
