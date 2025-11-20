using UnityEngine;
using UnityEngine.InputSystem;

public class UI_PlayerUI : MonoBehaviour
{
    private PlayerInput input;

    private void Awake()
    {
        input = GetComponent<PlayerInput>();
    }

    // Called automatically when PlayerInput is created
    private void Start()
    {
        var menu = FindFirstObjectByType<UI_MatchMenu>(FindObjectsInactive.Include);
        if (menu == null)
        {
            Debug.LogError("[UI_PlayerUI] No UI_MatchMenu found in scene!");
            return;
        }

        menu.RegisterPlayer(input);
    }

    // Called when UI Cancel is pressed (or PlayerInput destroyed)
    public void Leave()
    {
        var menu = FindFirstObjectByType<UI_MatchMenu>();
        if (menu != null)
            menu.UnregisterPlayer(input);

        Destroy(gameObject);
    }

    // Called by Input System using Send Messages or UnityEvents
    public void OnCancel()
    {
        var selector = GetComponentInChildren<UI_CharacterSelector>();
        
        // If selector exists and slot is not locked → do NOT leave the lobby
        if (selector != null && !selector.IsLocked)
        {
            selector.ClearAssignment();
            return;
        }

        Leave(); // full leave if locked or no slot
    }
}
