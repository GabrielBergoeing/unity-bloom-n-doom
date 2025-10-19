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
}
