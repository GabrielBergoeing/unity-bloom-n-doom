using TMPro;
using UnityEngine;

// Shows SteamLobby's connection status (LAN -> public IP -> hole punch cascade) so
// players/testers don't have to guess whether it's still trying or already gave up -
// that cascade can take 15+ seconds with nothing else on screen otherwise, which is
// what's been causing testers to assume it's frozen and close the app mid-attempt.
//
// Setup: drop this on any GameObject in CharacterSelectorOnline (where SteamLobby
// lives) and assign a TMP_Text in the Inspector. No other wiring needed - it just
// listens to SteamLobby.OnConnectStatusChanged while enabled.
public class UI_ConnectionStatus : MonoBehaviour
{
    [SerializeField] private TMP_Text statusText;

    private void OnEnable()
    {
        SteamLobby.OnConnectStatusChanged += HandleStatusChanged;
    }

    private void OnDisable()
    {
        SteamLobby.OnConnectStatusChanged -= HandleStatusChanged;
    }

    private void HandleStatusChanged(string message)
    {
        if (statusText != null)
            statusText.text = message;
    }
}
