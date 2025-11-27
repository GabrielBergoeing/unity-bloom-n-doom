using UnityEngine;
using UnityEngine.InputSystem;

public class SystemInputHandler : MonoBehaviour
{
    public void OnExitGame()
    {
        Debug.Log("ExitGame from Global input");
        Application.Quit();
    }

    public void OnBackToMainMenu()
    {
        Debug.Log("Return to main menu from Global input");
        GameManager.instance.ChangeScene("MainMenu");
    }
}
