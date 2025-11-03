using UnityEngine;

public class UI_MainMenu : MonoBehaviour
{
    public void Start()
    {
        //Fade Effect, sfx...
        transform.root.GetComponentInChildren<UI_FadeScreen>().FadeIn();
    }

    public void PlayBTN()
    {
        GameManager.instance.ChangeScene("MatchMenu");
    }

    public void QuitGameBTN()
    {
        Application.Quit();
    }

    public void KeyboardBTN()
    {
        GameManager.instance.ChangeScene("ControlConfig");
    }
    public void ControllerBTN()
    {
        GameManager.instance.ChangeScene("ControlConfig2");
    }
    public void MainMenuBTN()
    {
        GameManager.instance.ChangeScene("MainMenu");
    }

}
