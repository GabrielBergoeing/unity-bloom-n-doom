using UnityEngine;

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
        // Show tint or blur? (Optional)
        settingsPanel.SetActive(true);
    }

    public void HideSettingsOverlay()
    {
        settingsPanel.SetActive(false);
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}
