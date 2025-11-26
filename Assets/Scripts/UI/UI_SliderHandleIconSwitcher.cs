using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Slider))]
public class SliderHandleIconSwitcher : MonoBehaviour, ISelectHandler, IDeselectHandler
{
    [Header("Handle Images")]
    [SerializeField] private Image handleImage;
    [SerializeField] private Sprite normalSprite;
    [SerializeField] private Sprite zeroSprite;

    [Header("Optional Coloring")]
    [SerializeField] private bool changeColor = false;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color zeroColor = Color.gray;

    [Header("Selection Feedback")]
    [SerializeField] private bool scaleOnSelect = true;
    [SerializeField] private float selectedScale = 1.15f;
    [SerializeField] private float scaleSpeed = 10f;

    [SerializeField] private bool colorOnSelect = true;
    [SerializeField] private Color selectedHandleColor = Color.yellow;

    private Slider slider;
    private Vector3 originalScale;
    private Color originalColor;
    private bool isSelected;

    private void Awake()
    {
        slider = GetComponent<Slider>();
        slider.onValueChanged.AddListener(OnSliderValueChanged);

        originalScale = handleImage.rectTransform.localScale;
        originalColor = handleImage.color;

        UpdateHandleIcon(slider.value);
    }

    private void Update()
    {
        // Smooth scaling feedback
        if (scaleOnSelect)
        {
            Vector3 target = isSelected ? originalScale * selectedScale : originalScale;
            handleImage.rectTransform.localScale =
                Vector3.Lerp(handleImage.rectTransform.localScale, target, Time.unscaledDeltaTime * scaleSpeed);
        }
    }

    private void OnSliderValueChanged(float v) => UpdateHandleIcon(v);

    private void UpdateHandleIcon(float v)
    {
        bool isZero = Mathf.Approximately(v, slider.minValue);

        handleImage.sprite = isZero ? zeroSprite : normalSprite;

        if (changeColor && !isSelected)   // prevent conflict with highlight
            handleImage.color = isZero ? zeroColor : normalColor;
    }

    // ======= UI Selection EVENTS ========

    public void OnSelect(BaseEventData eventData)
    {
        isSelected = true;
        if (colorOnSelect)
            handleImage.color = selectedHandleColor;
    }

    public void OnDeselect(BaseEventData eventData)
    {
        isSelected = false;
        if (changeColor)
        {
            bool isZero = Mathf.Approximately(slider.value, slider.minValue);
            handleImage.color = isZero ? zeroColor : normalColor;
        }
        else
        {
            handleImage.color = originalColor;
        }
    }
}
