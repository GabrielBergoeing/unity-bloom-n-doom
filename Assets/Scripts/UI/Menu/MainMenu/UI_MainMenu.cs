using UnityEngine;

public class UI_MainMenu : MonoBehaviour
{
    private UIService UI => UIService.instance;
    public void PlayBTN()
    {
        UI.sfx.PlayOnConfirm();
        GameManager.instance.ChangeScene("OnlineMenu");
    }

    public void PlayOfflineBTN()
    {
        UI.sfx.PlayOnConfirm();
        GameManager.instance.ChangeScene("MatchMenu");
    }

    public void SettingsBTN()
    {
        UI.sfx.PlayOnToggle();
        UI.menu.ShowSettingsOverlay();
    }

    public void QuitGameBTN()
    {
        UI.sfx.PlayOnToggle();
        UI.menu.QuitGame();
    }

    public void BackBTN()
    {
        UI.sfx.PlayOnToggle();
        GameManager.instance.ChangeScene("MainMenu");
    }
    public void HoverBTN()
    {
        UI.sfx.PlayOnHover();
    }

    public void ToggleBTN()
    {
        UI.sfx.PlayOnToggle();
    }
}
