using UnityEngine;

public class UI_MainMenu : MonoBehaviour
{
    private UIService UI => UIService.instance;
    public void PlayBTN()
    {
        UI.sfx.PlayOnConfirm();
        GameManager.instance.ChangeScene("PlayMenu");
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

    //Online menu

    public void HostBTN()
    {
        UI.sfx.PlayOnConfirm();
        GameManager.instance.ChangeScene("OnlineLobby");
    }

    public void JoinBTN()
    {
        UI.sfx.PlayOnConfirm();
        //LOGIC TO AUTO CONNECT TO SERVER
        GameManager.instance.ChangeScene("OnlineLobby");
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
