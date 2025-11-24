using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UI_LevelDescription : MonoBehaviour
{
    [Header("Level Buttons (order must match descriptions)")]
    [SerializeField] private Button[] buttons;

    [Header("Corresponding Level Descriptions")]
    [TextArea(2, 5)]
    [SerializeField] private string[] descriptions;

    private TextMeshProUGUI descriptionText;

    private void Awake()
    {
        if (descriptionText == null)
            descriptionText = GetComponentInChildren<TextMeshProUGUI>(true);

        RegisterHoverEvents();
        SetDescription(-1); // default text
    }

    private void RegisterHoverEvents()
    {
        if (buttons == null || buttons.Length == 0) return;

        for (int i = 0; i < buttons.Length; i++)
        {
            int idx = i;
            var hover = buttons[i].gameObject.AddComponent<UIHoverExtention>();
            hover.Setup(() => SetDescription(idx));
        }
    }

    private void SetDescription(int index)
    {
        if (descriptionText == null) return;

        if (index < 0 || index >= descriptions.Length)
            descriptionText.text = "Select a level to see its details.";
        else
            descriptionText.text = descriptions[index];
    }
}
