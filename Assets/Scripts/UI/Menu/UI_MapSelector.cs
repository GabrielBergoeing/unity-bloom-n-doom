using UnityEngine;

public class UI_MapSelector : MonoBehaviour
{
    private UI_SFX sfx;
    public void Start()
    {
        sfx = GetComponent<UI_SFX>();
        transform.root.GetComponentInChildren<UI_FadeScreen>().FadeIn();
    }

    public void Level1BTN()
    {
        sfx.PlayOnConfirm();
        GameManager.instance.ChangeSceneWithLevel("LevelScene", "level1.json");
    }

    public void Level2BTN()
    {
        sfx.PlayOnConfirm();
        GameManager.instance.ChangeSceneWithLevel("LevelScene", "level2.json");
    }

    public void Level3BTN()
    {
        sfx.PlayOnConfirm();
        GameManager.instance.ChangeSceneWithLevel("LevelScene", "level3.json");
    }

    public void Level4BTN()
    {
        sfx.PlayOnConfirm();
        GameManager.instance.ChangeSceneWithLevel("LevelScene", "level4.json");
    }

    public void Level5BTN()
    {
        sfx.PlayOnConfirm();
        GameManager.instance.ChangeSceneWithLevel("LevelScene", "level5.json");
    }

}