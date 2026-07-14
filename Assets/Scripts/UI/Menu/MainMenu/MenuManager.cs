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

    private void Awake()
    {
        instance = this;
#if !UNITY_SERVER
        if (mainMenuPanel != null) mainCanvas = mainMenuPanel.GetComponent<CanvasGroup>();
        if (settingsPanel != null) settingsCanvas = settingsPanel.GetComponent<CanvasGroup>();
#endif
    }

    private void Start()
    {
#if !UNITY_SERVER
        if (AudioManager.instance != null) AudioManager.instance.StartBGM("bgm_menu");
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        if (settingsPanel != null) settingsPanel.SetActive(false);
#endif
    }

    public void ShowSettingsOverlay()
    {
#if !UNITY_SERVER
        settingsPanel.SetActive(true);
        selectFirstGameObject(settingsCanvas);
        mainCanvas.interactable = false;
#endif
    }

    public void HideSettingsOverlay()
    {
#if !UNITY_SERVER
        settingsPanel.SetActive(false);
        selectFirstGameObject(mainCanvas);
        mainCanvas.interactable = true;
#endif
    }

    public void QuitGame()
    {
        Application.Quit();
    }

    private void selectFirstGameObject(CanvasGroup cv)
    {
#if !UNITY_SERVER
        var ev = EventSystem.current;
        var first = cv.GetComponentInChildren<Selectable>(true);
        if (ev != null && first != null)
            ev.SetSelectedGameObject(first.gameObject);
#endif
    }
}