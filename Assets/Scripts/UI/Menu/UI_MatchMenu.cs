using System.Collections;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class UI_MatchMenu : MonoBehaviour
{
    [Header("Character Slots (4 max)")]
    [SerializeField] private UI_CharacterSelector[] slots = new UI_CharacterSelector[4];

    [Header("Settings")]
    [SerializeField] private InputActionAsset menuInputActions;
    [Range(1, 4)] [SerializeField] private int minimumPlayers = 1;
    [SerializeField] private float autoStartDelay = 2.0f;

    private readonly Dictionary<PlayerInput, UI_CharacterSelector> players = new();
    private Coroutine autoStartCoroutine;
    private UIService UI => UIService.instance;

    private void Awake()
    {
        foreach (var s in slots)
            if (s != null) s.SetEmptyVisuals();
    }

    private void OnDisable()
    {
        CancelAutoStart();
    }

    // ------------------- JOIN / LEAVE -------------------
    public void RegisterPlayer(PlayerInput pi)
    {
        var slot = FindFreeSlot();
        if (slot == null)
        {
            Debug.LogWarning("[MatchMenu] No free slot left. Rejecting player.");
            Destroy(pi.gameObject);
            return;
        }

        // optional: enforce action map
        pi.SwitchCurrentActionMap("UI");

        players[pi] = slot;
        slot.AssignPlayer(pi, OnSlotUpdated);
        EvaluateAutoStart();
    }

    public void UnregisterPlayer(PlayerInput pi)
    {
        if (!players.TryGetValue(pi, out var slot)) return;

        slot.ClearAssignment();
        players.Remove(pi);
        EvaluateAutoStart();
    }


    private UI_CharacterSelector FindFreeSlot()
    {
        return slots.FirstOrDefault(s => !s.IsOccupied);
    }

    // ------------------- AUTO START -------------------

    private void EvaluateAutoStart()
    {
        CancelAutoStart();

        var active = slots.Where(s => s.IsOccupied).ToArray();
        if (active.Length < minimumPlayers) return;

        if (active.Any(s => s.SelectedCharacter == null)) return;

        autoStartCoroutine = StartCoroutine(AutoStart());
    }

    private IEnumerator AutoStart()
    {
        float t = autoStartDelay;
        while (t > 0f)
        {
            if (!AllLockedValid())
            {
                autoStartCoroutine = null;
                yield break;
            }
            t -= Time.unscaledDeltaTime;
            yield return null;
        }

        autoStartCoroutine = null;
        StartMatch();
    }

    private bool AllLockedValid()
    {
        var active = slots.Where(s => s.IsOccupied);
        return active.All(s => s.SelectedCharacter != null);
    }

    private void CancelAutoStart()
    {
        if (autoStartCoroutine != null)
        {
            StopCoroutine(autoStartCoroutine);
            autoStartCoroutine = null;
        }
    }

    // ------------------- COMMENCE MATCH -------------------

    private void StartMatch()
    {
        UI.sfx.PlayOnConfirm();
        Debug.Log("[MatchMenu] Launching next scene.");

        // keep player order consistent: slot order = player order
        var ordered = slots.Where(s => s.IsOccupied)
                           .Select(s => s.AssignedPlayer)
                           .ToArray();

        PlayerInputService.instance.StoreLobbyPlayers(ordered);

        // write selected characters to config for gameplay spawning
        var configs = PlayerInputService.instance.Configs;
        for (int i = 0; i < ordered.Length; i++)
        {
            var selector = slots[i];
            configs[i].selectedCharacter = selector.SelectedCharacter;
        }

        GameManager.instance.StartMatchScene("MapSelector");
    }

    // callback from selector when state changes
    private void OnSlotUpdated(UI_CharacterSelector s) => EvaluateAutoStart();
}
