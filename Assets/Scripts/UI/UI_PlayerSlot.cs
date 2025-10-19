using UnityEngine;
using UnityEngine.InputSystem;

public class UI_PlayerSlot : MonoBehaviour
{
    public PlayerInput playerInput { get; private set; }

    private void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
    }

    public void OnJoin()
    {
        var menu = FindFirstObjectByType<UI_MatchMenu>();
        if (menu != null)
            menu.RegisterPlayer(this);
    }

    public void OnLeave()
    {
        OnCancel();
    }

    public void OnCancel()
    {
        if (PlayerInputManager.instance != null)
            PlayerInputManager.instance.playerLeftEvent.Invoke(playerInput);
        Destroy(playerInput.gameObject);
    }
}
