using UnityEngine;

public class UI_MainMenu : MonoBehaviour
{
    private UIService UI => UIService.instance;
    public void PlayBTN()
    {
        UI.sfx.PlayOnConfirm();
        GameManager.instance.ChangeScene("MatchMenu");
    }

    public void SettingsBTN()
    {
        UI.sfx.PlayOnConfirm();
        UI.menu.ShowSettingsOverlay();
    }

    public void QuitGameBTN()
    {
        UI.sfx.PlayOnConfirm();
        UI.menu.QuitGame();
    }
}
