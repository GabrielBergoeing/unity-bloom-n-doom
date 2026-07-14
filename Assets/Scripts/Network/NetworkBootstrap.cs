using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Instantiates the NetworkBootstrap prefab (NetworkManager + UnityTransport +
/// ConnectionManager + overlay UI) from Resources at game startup, so no scene
/// needs to contain networking objects.
/// </summary>
public static class NetworkBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        if (NetworkManager.Singleton != null)
            return;

        var prefab = Resources.Load<GameObject>("NetworkBootstrap");
        if (prefab == null)
        {
            Debug.LogError("[NetworkBootstrap] Missing Resources/NetworkBootstrap prefab. " +
                           "Run 'Tools > NGO Setup > Run Full Setup' in the editor once.");
            return;
        }

        var go = Object.Instantiate(prefab);
        go.name = "NetworkBootstrap";
        Object.DontDestroyOnLoad(go);
    }
}
