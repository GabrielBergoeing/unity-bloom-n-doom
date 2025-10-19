using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;

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

    private IEnumerator ChangeSceneCo(string sceneName)
    {
        UI_FadeScreen fadeScreen = FindFadeScreenUI();
        fadeScreen.FadeOut();
        yield return fadeScreen.fadeEffectCo;

        SceneManager.LoadScene(sceneName);

        fadeScreen = FindFadeScreenUI();
        fadeScreen.FadeIn();
    }

    private UI_FadeScreen FindFadeScreenUI()
    {
        if(UI.instance != null)
        {
            return UI.instance.fadeScreen;
        }
        return FindFirstObjectByType<UI_FadeScreen>();
    }

    public void ChangeScene(string sceneName)
    {
        StartCoroutine(ChangeSceneCo(sceneName));
    }
}
