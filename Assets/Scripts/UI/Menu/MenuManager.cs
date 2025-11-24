using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MenuManager : MonoBehaviour
{
    public static MenuManager instance;

    [Header("Panels")]
    public GameObject mainMenuPanel;
    public GameObject settingsPanel;

    private void Awake() => instance = this;

    private void Start()
    {
        // Main Menu is visible by default
        mainMenuPanel.SetActive(true);
        settingsPanel.SetActive(false);
    }

    public void ShowSettingsOverlay()
    {
        settingsPanel.SetActive(true);
        
        // Enable raycast block
        var blocker = settingsPanel.GetComponent<Image>();
        if (blocker != null) blocker.raycastTarget = true;

        // Force selection to first settings element
        var ev = EventSystem.current;
        var first = settingsPanel.GetComponentInChildren<Selectable>(true);
        if (ev != null && first != null)
            ev.SetSelectedGameObject(first.gameObject);
    }

    public void HideSettingsOverlay()
    {
        settingsPanel.SetActive(false);

        // Remove raycast block
        var blocker = settingsPanel.GetComponent<Image>();
        if (blocker != null) blocker.raycastTarget = false;

        // Return focus to Main Menu
        var ev = EventSystem.current;
        var target = mainMenuPanel.GetComponentInChildren<Selectable>(true);
        if (ev != null && target != null)
            ev.SetSelectedGameObject(target.gameObject);
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}
