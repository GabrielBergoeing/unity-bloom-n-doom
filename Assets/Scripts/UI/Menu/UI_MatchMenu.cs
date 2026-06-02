using System.Collections;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Mirror;

public class UI_MatchMenu : MonoBehaviour
{
    #region Variables
    [Header("Character Slots (4 max)")]
    [SerializeField] private UI_CharacterSelector[] slots = new UI_CharacterSelector[4];

    [Header("Settings")]
    [SerializeField] private InputActionAsset menuInputActions;
    [Range(1, 4)] [SerializeField] private int minimumPlayers = 1;
    [SerializeField] private float autoStartDelay = 2.0f;

    private readonly Dictionary<PlayerInput, UI_CharacterSelector> players = new();
    private Coroutine autoStartCoroutine;
    private UIService UI => UIService.instance;

    private bool isOnlineMode = false; // setear desde UI
    #endregion

    private void Awake()
    {
        foreach (var s in slots)
            if (s != null) s.SetEmptyVisuals();
    }

    private void OnDisable()
    {
        CancelAutoStart();
    }

    #region Join/Leave
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
    #endregion


    #region Autostart Timer
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
    #endregion


    #region Start Match
    private void StartMatch()
    {
        UI.sfx.PlayOnConfirm();
        Debug.Log("[MatchMenu] Launching next scene.");

        var ordered = slots.Where(s => s.IsOccupied)
                           .Select(s => s.AssignedPlayer)
                           .ToArray();

        // Guardar configuraciones de los players (maneja nulls para slots remotos)
        PlayerInputService.instance.StoreLobbyPlayers(ordered);

        var configs = PlayerInputService.instance.Configs;
        for (int i = 0; i < ordered.Length; i++)
            configs[i].selectedCharacter = slots[i].SelectedCharacter;

        if (isOnlineMode)
        {
            // Validaciones para online
            if (NetworkManager.singleton == null)
            {
                Debug.LogError("[MatchMenu] No NetworkManager found for online mode.");
                return;
            }

            if (NetworkManager.singleton.playerPrefab == null)
            {
                Debug.LogError("[MatchMenu] NetworkManager.playerPrefab is empty. Asigna un prefab o configura OnServerAddPlayer.");
                return;
            }

            // Evitar joins locales que creen PlayerInput redundantes
            if (PlayerInputManager.instance != null)
                PlayerInputManager.instance.enabled = false;

            // Arranca host (o client según la UI)
            NetworkManager.singleton.StartHost();
        }
        else
        {
            // Local: PlayerInputManager debe estar activo para joins; la escena de juego hará spawn local con PlayerInputService
            if (PlayerInputManager.instance != null)
                PlayerInputManager.instance.enabled = true;
        }

        GameManager.instance.StartMatchScene("MapSelector");
    }

    // callback from selector when state changes
    private void OnSlotUpdated(UI_CharacterSelector s) => EvaluateAutoStart();
    #endregion
}
