using System.Collections.Generic;
using UnityEngine;

public class UI_MapSelector : MonoBehaviour
{
    private UIService UI => UIService.instance;
    [Header("Levels Data")]
    [SerializeField] private List<LevelData> levels;

    public void Level1BTN() => SelectLevel(1);
    public void Level2BTN() => SelectLevel(2);
    public void Level3BTN() => SelectLevel(3);
    public void Level4BTN() => SelectLevel(4);
    public void Level5BTN() => SelectLevel(5);

    public void SelectLevel(int index)
    {
        var chosenLevel = levels[index-1];
        AudioManager.instance.StopBGM();
        UI.sfx.PlayOnConfirm();
        
        GameManager.instance.ChangeSceneWithLevel("LevelScene", chosenLevel);
        AudioManager.instance.StartBGM(chosenLevel.bgmTrackName);
    }
}
