using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

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
        UI_FadeScreen fadeScreen = FindFadeScreenUI();
        
        if (fadeScreen != null)
        {
            fadeScreen.FadeIn();
        }
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
