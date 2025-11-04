using System.Linq;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class UI_MapSelector : MonoBehaviour
{
    public void Start()
    {
        //Fade Effect, sfx...
        transform.root.GetComponentInChildren<UI_FadeScreen>().FadeIn();
    }

    public void Level1BTN()
    {
        GameManager.instance.ChangeSceneWithLevel("LevelScene", "level1.json");
    }

    public void Level2BTN()
    {
        GameManager.instance.ChangeSceneWithLevel("LevelScene", "level2.json");
    }

    public void Level3BTN()
    {
        GameManager.instance.ChangeSceneWithLevel("LevelScene", "level3.json");
    }

    public void Level4BTN()
    {
        GameManager.instance.ChangeSceneWithLevel("LevelScene", "level4.json");
    }

    public void Level5BTN()
    {
        GameManager.instance.ChangeSceneWithLevel("LevelScene", "level5.json");
    }

}