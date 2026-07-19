using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Online version of the map selector.
/// Only the host (server) can pick a map; the selection uses
/// OnlineNetworkManager.SelectLevel() which syncs level data to all
/// clients and calls ServerChangeScene so everyone transitions together.
///
/// Non-host clients see the buttons as non-interactable ("Waiting for host").
/// </summary>
public class UI_MapSelectorOnline : MonoBehaviour
{
    [Header("Scene to load after selection")]
    [SerializeField] private string onlineLevelScene = "LevelSceneOnline";

    [Header("Optional: buttons to disable for non-host")]
    [SerializeField] private List<Button> mapButtons = new();

    private void Start()
    {
        // Clients cannot pick — disable their buttons
        bool isHost = NetworkServer.active;
        foreach (var btn in mapButtons)
        {
            if (btn != null)
                btn.interactable = isHost;
        }

        if (!isHost)
        {
            Debug.Log("[MapSelectorOnline] Client: waiting for host to choose map.");
            return;
        }

        // Without this, a gamepad/keyboard user has nothing selected on entering this
        // screen and Navigate does nothing until they touch a mouse first.
        if (mapButtons.Count > 0 && mapButtons[0] != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(mapButtons[0].gameObject);
    }

    // ── Called by UI buttons ──
    public void Level1BTN() => SelectLevel(0);
    public void Level2BTN() => SelectLevel(1);
    public void Level3BTN() => SelectLevel(2);
    public void Level4BTN() => SelectLevel(3);
    public void Level5BTN() => SelectLevel(4);

    public void SelectLevel(int index)
    {
        if (!NetworkServer.active)
            return;

        if (NetworkManager.singleton is OnlineNetworkManager mgr)
        {
            if (AudioManager.instance != null)
            {
                LevelData level = mgr.GetLevel(index);
                if (level != null)
                {
                    AudioManager.instance.StopBGM();
                    AudioManager.instance.StartBGM(level.bgmTrackName);
                }
            }

            mgr.SelectLevel(index, onlineLevelScene);
        }
        else
        {
            // Fallback if OnlineNetworkManager isn't used — still syncs scene
            Debug.LogWarning("[MapSelectorOnline] NetworkManager is not OnlineNetworkManager. Level data won't be synced to clients.");
            NetworkManager.singleton?.ServerChangeScene(onlineLevelScene);
        }
    }
}
