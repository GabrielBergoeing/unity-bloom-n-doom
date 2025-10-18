using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class UI_MatchMenu : MonoBehaviour
{
    [Header("Player Panels (order: TL, TR, BL, BR)")]
    [SerializeField] private GameObject[] playerPanels;

    [Header("Start Button")]
    [SerializeField] private Button startMatchButton;

    [Header("Input Settings")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private string actionMapToUse = "UI";

    private readonly Dictionary<int, GameObject> activePlayers = new();

    private void Awake()
    {
        Debug.Log("[MatchMenu] Awake called.");
        // Safety: Ensure all panels start hidden
        for (int i = 0; i < playerPanels.Length; i++)
        {
            if (playerPanels[i] != null)
            {
                panelDebug(i, "Setting inactive in Awake");
                playerPanels[i].SetActive(false);
            }
            else
            {
                Debug.LogWarning($"[MatchMenu] Player panel {i} is not assigned in Inspector!");
            }
        }

        if (startMatchButton != null)
        {
            startMatchButton.interactable = false;
            Debug.Log("[MatchMenu] Start button set to inactive in Awake.");
        }
        else
        {
            Debug.LogWarning("[MatchMenu] Start button reference missing in Inspector!");
        }
    }

    private void OnEnable()
    {
        Debug.Log("[MatchMenu] OnEnable called, starting WaitForInputManager coroutine.");
        StartCoroutine(WaitForInputManager());
    }

    private IEnumerator WaitForInputManager()
    {
        yield return new WaitUntil(() => PlayerInputManager.instance != null);

        Debug.Log("[MatchMenu] InputManager found, subscribing to join/leave events.");
        PlayerInputManager.instance.onPlayerJoined += HandlePlayerJoined;
        PlayerInputManager.instance.onPlayerLeft += HandlePlayerLeft;

        UpdateStartButton();
    }

    private void OnDisable()
    {
        if (PlayerInputManager.instance == null) return;
        PlayerInputManager.instance.onPlayerJoined -= HandlePlayerJoined;
        PlayerInputManager.instance.onPlayerLeft -= HandlePlayerLeft;
        Debug.Log("[MatchMenu] Unsubscribed from InputManager events.");
    }

    public void RegisterPlayer(UI_PlayerSlot slot)
    {
        var player = slot.playerInput;
        Debug.Log($"[MatchMenu] RegisterPlayer called for Player {player.playerIndex}");

        if (player.playerIndex >= playerPanels.Length)
        {
            Debug.LogWarning($"Player {player.playerIndex} exceeds panel slots!");
            return;
        }

        var panel = playerPanels[player.playerIndex];
        panel.SetActive(true);
        var text = panel.GetComponentInChildren<Text>();
        if (text != null)
            text.text = $"Player {player.playerIndex + 1} Ready!";

        activePlayers[player.playerIndex] = panel;
        UpdateStartButton();
    }


    private void HandlePlayerJoined(PlayerInput player)
    {
        Debug.Log($"[MatchMenu] Player {player.playerIndex} joined ({player.devices[0].displayName})");

        // Assign Input Action Asset
        if (inputActions != null)
        {
            player.actions = inputActions;
            Debug.Log($"[MatchMenu] Assigned InputActionAsset '{inputActions.name}' to Player {player.playerIndex}");
        }

        // Switch to desired action map
        if (!string.IsNullOrEmpty(actionMapToUse) && player.actions != null)
        {
            var map = player.actions.FindActionMap(actionMapToUse, true);
            if (map != null)
            {
                player.SwitchCurrentActionMap(actionMapToUse);
                Debug.Log($"[MatchMenu] Switched Player {player.playerIndex} to action map '{actionMapToUse}'");
            }
            else
            {
                Debug.LogWarning($"[MatchMenu] Action map '{actionMapToUse}' not found for Player {player.playerIndex}");
            }
        }

        // Assign corner panel
        if (player.playerIndex >= playerPanels.Length)
        {
            Debug.LogWarning($"[MatchMenu] Player {player.playerIndex} exceeds panel slots.");
            return;
        }

        var panel = playerPanels[player.playerIndex];
        if (panel == null)
        {
            Debug.LogError($"[MatchMenu] Missing panel reference for Player {player.playerIndex}!");
            return;
        }

        // Activate panel visually
        panel.SetActive(true);
        panelDebug(player.playerIndex, "Activated on join");

        // Update text
        var text = panel.GetComponentInChildren<Text>();
        if (text != null)
        {
            text.text = $"Player {player.playerIndex + 1} Ready!";
            Debug.Log($"[MatchMenu] Updated text for Player {player.playerIndex} panel.");
        }

        activePlayers[player.playerIndex] = panel;
        Debug.Log($"[MatchMenu] Active players count: {activePlayers.Count}");
        UpdateStartButton();
    }

    private void HandlePlayerLeft(PlayerInput player)
    {
        Debug.Log($"[MatchMenu] Player {player.playerIndex} left.");

        if (activePlayers.TryGetValue(player.playerIndex, out var panel))
        {
            panel.SetActive(false);
            panelDebug(player.playerIndex, "Deactivated on leave");
            activePlayers.Remove(player.playerIndex);
            Debug.Log($"[MatchMenu] Removed Player {player.playerIndex} from activePlayers. Count now: {activePlayers.Count}");
        }

        UpdateStartButton();
    }

    private void UpdateStartButton()
    {
        if (startMatchButton == null)
        {
            Debug.LogWarning("[MatchMenu] StartMatchButton not assigned!");
            return;
        }

        bool interactable = activePlayers.Count >= 2;
        startMatchButton.interactable = interactable;
        Debug.Log($"[MatchMenu] UpdateStartButton called. Active players: {activePlayers.Count}. Start button interactable: {interactable}");
    }

    private void panelDebug(int index, string message)
    {
        Debug.Log($"[MatchMenu] Panel {index}: {message}");
    }
}
