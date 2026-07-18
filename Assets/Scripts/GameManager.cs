using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;
    public LevelData currentLevel;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
#if UNITY_SERVER
        // Headless: there's no human host to click through the menu/character-select
        // flow, so jump straight to the scene where NetworkManager/GameLiftServerManager
        // live and start listening. Real players still go through the normal menu ->
        // character select flow themselves on their own client builds - the dedicated
        // server just needs to already be there and ready before anyone tries to join.
        SceneManager.LoadScene("CharacterSelectorOnline");
        return;
#endif
        UI_FadeScreen fadeScreen = FindFadeScreenUI();

        if (fadeScreen != null)
        {
            fadeScreen.FadeIn();
        }
    }

    private void Update()
    {
        // For testing scene changes. Legacy Input.GetKeyDown ignores UI focus, so this must
        // not fire while the user is typing into a text field (e.g. join-code/IP inputs) or
        // every backspace keystroke would kick them back to MainMenu mid-typing.
        if (Input.GetKeyDown(KeyCode.Backspace) && !IsTextInputFocused())
        {
            Debug.Log("[GameManager] Scene Change to MainMenu!");
            ChangeScene("MainMenu");
        }
        else if (Input.GetKeyDown(KeyCode.Escape))
        {
            Debug.Log("[GameManager] App Closed!");
            Application.Quit();
        }
    }

    private bool IsTextInputFocused()
    {
        GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (selected == null) return false;

        return selected.GetComponent<TMP_InputField>() != null || selected.GetComponent<InputField>() != null;
    }

    private IEnumerator ChangeSceneCo(string sceneName)
    {
        UI_FadeScreen fadeScreen = FindFadeScreenUI();

        if (fadeScreen != null)
        {
            fadeScreen.FadeOut();
            yield return fadeScreen.fadeEffectCo;
        }

        SceneManager.LoadScene(sceneName);
        yield return null;
        Debug.Log("[GameManager] Scene Change!");

        fadeScreen = FindFadeScreenUI();

        if (fadeScreen != null)
        {
            fadeScreen.FadeIn();
            yield return fadeScreen.fadeEffectCo;
        }
    }

    private UI_FadeScreen FindFadeScreenUI()
    {
        if (UIService.instance != null)
        {
            return UIService.instance.fade;
        }
        return FindFirstObjectByType<UI_FadeScreen>();
    }

    public void ChangeScene(string sceneName)
    {
        StartCoroutine(ChangeSceneCo(sceneName));
    }

    public void ChangeSceneWithLevel(string sceneName, LevelData levelFile)
    {
        currentLevel = levelFile;
        ChangeScene(sceneName);
    }

    public void StartMatchScene(string sceneName)
    {
        ChangeScene(sceneName);
    }
}
