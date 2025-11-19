using UnityEngine;

public class UI_MainMenu : UI
{
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
