using Mirror;
using UnityEngine;

/// <summary>
/// Placed in LevelSceneOnline. OnlineMatchManager (NetworkBehaviour scene object)
/// self-starts in OnStartServer — this component just logs if it's missing.
/// Do NOT put MatchManager or GameSceneLoader in the online level scene.
/// </summary>
public class OnlineLevelSceneManager : MonoBehaviour
{
    private void Start()
    {
        // Mirror's ServerChangeScene bypasses GameManager.ChangeScene(), so the
        // scene's fade screen (Awake()'s to opaque black) never gets faded in. Do it here.
        UI_FadeScreen fadeScreen = UIService.instance != null ? UIService.instance.fade : FindFirstObjectByType<UI_FadeScreen>();
        if (fadeScreen != null)
            fadeScreen.FadeIn();

        if (!NetworkClient.active && !NetworkServer.active)
        {
            Debug.LogWarning("[OnlineLevelSceneManager] No active Mirror session. Skipping online init.");
            return;
        }

        if (OnlineMatchManager.instance == null)
            Debug.LogWarning("[OnlineLevelSceneManager] OnlineMatchManager not found — add it as a scene NetworkBehaviour in LevelSceneOnline.");
    }
}
