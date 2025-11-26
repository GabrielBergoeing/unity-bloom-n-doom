using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MenuManager : MonoBehaviour
{
    public static MenuManager instance;

    [Header("Panels")]
    public GameObject mainMenuPanel;
    public GameObject settingsPanel;

    private CanvasGroup mainCanvas;
    private CanvasGroup settingsCanvas;

    private void Awake(){
        instance = this;
        mainCanvas = mainMenuPanel.GetComponent<CanvasGroup>();
        settingsCanvas = settingsPanel.GetComponent<CanvasGroup>();
    }

    private void Start()
    {
        // Main Menu is visible by default
        AudioManager.instance.StartBGM("bgm_menu");
        mainMenuPanel.SetActive(true);
        settingsPanel.SetActive(false);
    }

    public void ShowSettingsOverlay()
    {
        settingsPanel.SetActive(true);
        selectFirstGameObject(settingsCanvas);
        mainCanvas.interactable = false;
    }

    public void HideSettingsOverlay()
    {
        settingsPanel.SetActive(false);
        selectFirstGameObject(mainCanvas);
        mainCanvas.interactable = true;
    }

    public void QuitGame()
    {
        Application.Quit();
    }

    private void selectFirstGameObject(CanvasGroup cv)
    {
        var ev = EventSystem.current;
        var first = cv.GetComponentInChildren<Selectable>(true);
        if (ev != null && first != null)
            ev.SetSelectedGameObject(first.gameObject);
    }
}
