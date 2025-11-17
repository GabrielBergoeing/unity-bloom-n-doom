using UnityEngine;

public class UI_MainMenu : MonoBehaviour
{
    private UI_SFX sfx;
    public void Start()
    {
        sfx = GetComponent<UI_SFX>();
        //AudioManager.instance.StartBGM("bg_menu");
        transform.root.GetComponentInChildren<UI_FadeScreen>().FadeIn();
    }

    public void PlayBTN()
    {
        sfx.PlayOnConfirm();
        GameManager.instance.ChangeScene("MatchMenu");
    }

    public void QuitGameBTN()
    {
        sfx.PlayOnConfirm();
        Application.Quit();
    }

    public void KeyboardBTN()
    {
        sfx.PlayOnConfirm();
        GameManager.instance.ChangeScene("ControlConfig");
    }
    public void ControllerBTN()
    {
        sfx.PlayOnConfirm();
        GameManager.instance.ChangeScene("ControlConfig2");
    }

    public void SettingsBTN()
    {
        sfx.PlayOnConfirm();
        GameManager.instance.ChangeScene("SettingsMenu");
    }

    public void MainMenuBTN()
    {
        sfx.PlayOnConfirm();
        GameManager.instance.ChangeScene("MainMenu");
    }

}
