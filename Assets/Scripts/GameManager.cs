using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    // Store player device info for respawning
    public readonly List<PlayerConfiguration> playerConfigs = new();

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
        if (UI.instance != null)
        {
            return UI.instance.fadeScreen;
        }
        return FindFirstObjectByType<UI_FadeScreen>();
    }

    public void RegisterPlayers(PlayerInput[] lobbyPlayers)
    {
        playerConfigs.Clear();

        foreach (var p in lobbyPlayers)
        {
            if (p.devices.Count == 0) continue;

            playerConfigs.Add(new PlayerConfiguration
            {
                device = p.devices[0],
                controlScheme = p.currentControlScheme
            });

            Destroy(p.gameObject);
        }

        Debug.Log($"[GameManager] Stored {playerConfigs.Count} players before scene change");
    }

    public void ChangeScene(string sceneName)
    {
        StartCoroutine(ChangeSceneCo(sceneName));
    }

    public void StartMatchScene(string sceneName)
    {
        ChangeScene(sceneName);
    }
}
