using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UI_MapSelector : MonoBehaviour
{
    [SerializeField] private string levelSceneName = "LevelScene";
    private UIService UI => UIService.instance;
    [Header("Levels Data")]
    [SerializeField] private List<LevelData> levels;

    private void Start()
    {
        // Without this, a gamepad/keyboard user has nothing selected on entering this
        // screen and Navigate does nothing until they touch a mouse first.
        var first = GetComponentInChildren<Selectable>(true);
        if (first != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(first.gameObject);
    }

    public void Level1BTN() => SelectLevel(1);
    public void Level2BTN() => SelectLevel(2);
    public void Level3BTN() => SelectLevel(3);
    public void Level4BTN() => SelectLevel(4);
    public void Level5BTN() => SelectLevel(5);
    public void LevelTestBTN() => SelectLevel(6);

    public void SelectLevel(int index)
    {
        var chosenLevel = levels[index-1];
        AudioManager.instance.StopBGM();
        UI.sfx.PlayOnConfirm();
        
        GameManager.instance.ChangeSceneWithLevel(levelSceneName, chosenLevel);
        AudioManager.instance.StartBGM(chosenLevel.bgmTrackName);
    }
}
