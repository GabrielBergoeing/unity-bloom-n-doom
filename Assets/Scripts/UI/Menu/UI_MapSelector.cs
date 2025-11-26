using UnityEngine;

public class UI_MapSelector : MonoBehaviour
{
    private UIService UI => UIService.instance;

    public void Level1BTN() => SelectLevel("level1.json");
    public void Level2BTN() => SelectLevel("level2.json");
    public void Level3BTN() => SelectLevel("level3.json");
    public void Level4BTN() => SelectLevel("level4.json");
    public void Level5BTN() => SelectLevel("level5.json");

    private void SelectLevel(string levelFile)
    {
        AudioManager.instance.StopBGM();
        UI.sfx.PlayOnConfirm();
        GameManager.instance.ChangeSceneWithLevel("LevelScene", levelFile);
        AudioManager.instance.StartBGM("bgm_level1");
    }
}
