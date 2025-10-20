using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.EventSystems;

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

    private bool ready = false;

    private void Awake()
    {
        foreach (var panel in playerPanels)
        {
            if (panel != null) panel.SetActive(false);
        }

        if (startMatchButton != null)
        {
            startMatchButton.interactable = false;
        }
    }

    private void OnEnable()
    {
        StartCoroutine(WaitForInputManager());
    }

    private IEnumerator WaitForInputManager()
    {
        yield return new WaitUntil(() => PlayerInputManager.instance != null);

        PlayerInputManager.instance.onPlayerJoined += HandlePlayerJoined;
        PlayerInputManager.instance.onPlayerLeft += HandlePlayerLeft;
        UpdateStartButton();
    }

    private void OnDisable()
    {
        if (PlayerInputManager.instance == null) return;
        PlayerInputManager.instance.onPlayerJoined -= HandlePlayerJoined;
        PlayerInputManager.instance.onPlayerLeft -= HandlePlayerLeft;
    }

    public void RegisterPlayer(UI_PlayerSlot slot)
    {
        var player = slot.playerInput;

        if (player.playerIndex >= playerPanels.Length)
        {
            Debug.LogWarning($"Player {player.playerIndex} exceeds panel slots!");
            return;
        }

        AssignPanel(player);
    }

    private void HandlePlayerJoined(PlayerInput player)
    {
        if (inputActions != null)
            player.actions = inputActions;

        if (!string.IsNullOrEmpty(actionMapToUse))
            player.SwitchCurrentActionMap(actionMapToUse);

        AssignPanel(player);
    }

    private void AssignPanel(PlayerInput player)
    {
        if (player.playerIndex >= playerPanels.Length) return;

        var panel = playerPanels[player.playerIndex];
        if (panel == null) return;

        panel.SetActive(true);

        // Change txt
        var tmp = panel.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null)
            tmp.text = $"Player {player.playerIndex + 1} Ready!";
        else
        {
            var tmp3d = panel.GetComponentInChildren<TextMeshPro>();
            if (tmp3d != null)
                tmp3d.text = $"Player {player.playerIndex + 1} Ready!";
        }

        activePlayers[player.playerIndex] = panel;
        UpdateStartButton();
    }

    private void HandlePlayerLeft(PlayerInput player)
    {
        if (activePlayers.TryGetValue(player.playerIndex, out var panel))
        {
            if (panel != null)
                panel.SetActive(false);
            activePlayers.Remove(player.playerIndex);
        }

        UpdateStartButton();
    }

    private void UpdateStartButton()
    {
        if (startMatchButton == null) return;

        ready = activePlayers.Count >= 2;
        startMatchButton.interactable = ready;

        if (ready)
            EventSystem.current.SetSelectedGameObject(startMatchButton.gameObject);
    }

    public void StartMatch()
    {
        if (ready)
            GameManager.instance.ChangeScene("SampleScene");
    }
}
