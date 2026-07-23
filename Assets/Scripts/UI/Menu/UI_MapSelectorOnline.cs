using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Online version of the map selector.
/// Any connected client can pick a map - the request travels to the server
/// (OnlineNetworkManager.OnLevelSelectRequested), which applies it and calls
/// ServerChangeScene so everyone transitions together. This has to go over the
/// network rather than being applied directly by whoever clicks: under a P2P
/// host that happens to work locally (the host's own process is the server), but
/// under a true dedicated server (GameLift) no clicking player's process is ever
/// the server, so a direct call would silently no-op for every real player.
/// </summary>
public class UI_MapSelectorOnline : MonoBehaviour
{
    [Header("Scene to load after selection")]
    [SerializeField] private string onlineLevelScene = "LevelSceneOnline";

    [Header("Buttons")]
    [SerializeField] private List<Button> mapButtons = new();

    private void Start()
    {
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
    public void LevelTestBTN() => SelectLevel(5);

    public void SelectLevel(int index)
    {
        NetworkClient.Send(new LevelSelectRequestMessage { levelIndex = index, levelSceneName = onlineLevelScene });
    }
}
