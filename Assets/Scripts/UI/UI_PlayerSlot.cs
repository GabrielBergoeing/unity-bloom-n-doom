using UnityEngine;
using UnityEngine.InputSystem;

public class UI_PlayerSlot : MonoBehaviour
{
    public PlayerInput playerInput { get; private set; }

    private void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
        Debug.Log($"[UI_PlayerSlot] Awake called for PlayerInput {playerInput.playerIndex}");
    }

    // Called via Send Messages from Input Action
    public void OnJoin()
    {
        Debug.Log($"[UI_PlayerSlot] Player {playerInput.playerIndex} pressed Join button with device {playerInput.devices[0].displayName}");

        UI_MatchMenu matchMenu = FindObjectOfType<UI_MatchMenu>();
        if (matchMenu != null)
        {
            matchMenu.RegisterPlayer(this); // safe, parameterless call
            Debug.Log($"[UI_PlayerSlot] Notified MatchMenu of Player {playerInput.playerIndex} join.");
        }
    }

    public void OnLeave()
    {
        Debug.Log($"[UI_PlayerSlot] Player {playerInput.playerIndex} leaving lobby");

        PlayerInputManager.instance.playerPrefab = null; // Safety
        Destroy(gameObject); // triggers onPlayerLeft in MatchMenu
    }

    public void OnCancel()
    {
        Debug.Log($"[UI_PlayerSlot] Cancel action received for Player {playerInput.playerIndex}");
        Destroy(gameObject);
    }
}
