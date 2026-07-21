using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public class PlayerInputService : MonoBehaviour
{
    public static PlayerInputService instance;

    // Runtime PlayerInput references
    private readonly List<PlayerInput> players = new();
    public IReadOnlyList<PlayerInput> Players => players;

    // Cached config data (device + scheme)
    private readonly List<PlayerConfiguration> configs = new();
    public IReadOnlyList<PlayerConfiguration> Configs => configs;

    // Online character selection snapshot (slot order)
    private readonly List<CharacterData> onlineSelectedCharacters = new();
    public IReadOnlyList<CharacterData> OnlineSelectedCharacters => onlineSelectedCharacters;

    public InputDevice LastUsedDevice { get; private set; }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        // safe-guard: PlayerInputManager may not exist in editor startup order
        if (PlayerInputManager.instance != null)
        {
            PlayerInputManager.instance.onPlayerJoined += HandlePlayerJoined;
            PlayerInputManager.instance.onPlayerLeft += HandlePlayerLeft;
        }
    }

    private void OnEnable()
    {
        // GLOBAL input tracking (for menus when no player exists)
        InputSystem.onEvent += OnAnyInputEvent;
    }

    private void OnDisable()
    {
        InputSystem.onEvent -= OnAnyInputEvent;
    }

    private void OnDestroy()
    {
        if (PlayerInputManager.instance != null)
        {
            PlayerInputManager.instance.onPlayerJoined -= HandlePlayerJoined;
            PlayerInputManager.instance.onPlayerLeft -= HandlePlayerLeft;
        }

        for (int i = 0; i < players.Count; i++)
        {
            if (players[i] != null)
                players[i].onControlsChanged -= OnControlsChanged;
        }
    }

    // ======================================================
    //  GLOBAL DEVICE TRACKING
    // ======================================================
    private void OnAnyInputEvent(InputEventPtr eventPtr, InputDevice device)
    {
        if (device == null) return;

        string layout = device.layout?.ToLower() ?? "";
        if (layout.Contains("sensor") || device is Touchscreen || device is Mouse)
            return;

        if (!(device is Keyboard || device is Gamepad))
            return;

        bool changed = LastUsedDevice != device;
        LastUsedDevice = device;

        if (!changed) return;

        // Find settings menu safely
        var settings = FindAnyObjectByType<UI_SettingsMenu>();
        if (settings == null || !settings.gameObject.activeInHierarchy)
            return;

        if (settings.IsControlsTabOpen)
            settings.RefreshIfActiveAndOnControlsTab();

        RefreshUIInputModule();
    }

    private void RefreshUIInputModule()
    {
        var uiModule = FindFirstObjectByType<UnityEngine.InputSystem.UI.InputSystemUIInputModule>(FindObjectsInactive.Include);
        if (uiModule != null)
        {
            uiModule.actionsAsset = uiModule.actionsAsset; // force refresh
            Debug.Log("[InputService] UI InputModule refreshed.");
        }
    }

    // ======================================================
    //  PLAYER JOIN/LEAVE
    // ======================================================
    private void HandlePlayerJoined(PlayerInput pi)
    {
        if (MatchManager.instance != null && MatchManager.instance.isMatchRunning)
        {
            Debug.Log("[PlayerInputService] Player join blocked (match is running).");
            Destroy(pi.gameObject);
            return;
        }

        // Normal logic:
        if (!players.Contains(pi))
        {
            players.Add(pi);
            pi.onControlsChanged += OnControlsChanged;
        }
    }


    private void HandlePlayerLeft(PlayerInput pi)
    {
        if (players.Contains(pi))
        {
            players.Remove(pi);
        }
    }

    private void OnControlsChanged(PlayerInput pi)
    {
        if (pi.devices.Count > 0)
        {
            LastUsedDevice = pi.devices[0];
            RefreshSettingsTab();
        }
    }

    private void RefreshSettingsTab()
    {
        var settings = FindAnyObjectByType<UI_SettingsMenu>();
        settings?.RefreshIfActiveAndOnControlsTab();
    }

    // ======================================================
    //  CONFIG MANAGEMENT (used by UI_MatchMenu)
    // ======================================================
    public void StoreLobbyPlayers(PlayerInput[] lobbyPlayers)
    {
        // Stop duplicate join events
        if (PlayerInputManager.instance != null)
            PlayerInputManager.instance.onPlayerJoined -= HandlePlayerJoined;

        configs.Clear();
        players.Clear();

        foreach (var p in lobbyPlayers)
        {
            // If slot represents a remote selection (null PlayerInput), keep a placeholder config
            if (p == null)
            {
                configs.Add(new PlayerConfiguration());
                continue;
            }

            // If the PlayerInput has no devices, store placeholder and destroy instance
            if (p.devices.Count == 0)
            {
                configs.Add(new PlayerConfiguration());
                Destroy(p.gameObject);
                continue;
            }

            configs.Add(new PlayerConfiguration
            {
                device = p.devices[0],
                controlScheme = p.currentControlScheme,
                selectedCharacter = p.GetComponent<UI_CharacterSelector>()?.SelectedCharacter
            });

            Debug.Log($"[PlayerInputService] Captured slot {configs.Count - 1}: device={p.devices[0].displayName} ({p.devices[0].path}), scheme={p.currentControlScheme}, allDevices=[{string.Join(", ", p.devices)}]");

            Destroy(p.gameObject);
        }

        Debug.Log($"[PlayerInputService] Stored {configs.Count} player configs.");
    }

    // ======================================================
    //  LOOKUP HELPERS
    // ======================================================
    public PlayerInput GetPlayerByIndex(int index)
    {
        if (index < 0 || index >= players.Count) return null;
        return players[index];
    }

    public PlayerInput GetPlayerByDevice(InputDevice device)
    {
        foreach (var p in players)
        {
            if (p.OwnsDevice(device)) return p;
        }
        return null;
    }

    public PlayerConfiguration GetConfig(int index)
    {
        if (index < 0 || index >= configs.Count) return null;
        return configs[index];
    }

    public InputDevice GetActiveDevice()
    {
        // 1. Device used most recently (global input or player input)
        if (LastUsedDevice != null) return LastUsedDevice;

        // 2. Fallback: Keyboard / Mouse
        if (Keyboard.current != null) return Keyboard.current;
        if (Mouse.current != null) return Mouse.current;

        // 3. Fallback: Gamepad
        if (Gamepad.current != null) return Gamepad.current;

        // 4. No device → UI shows generic icons
        return null;
    }

    public void ResetForGameplay()
    {
        players.Clear();
    }

    public void StoreOnlineSelections(IReadOnlyList<CharacterData> selectedCharacters)
    {
        onlineSelectedCharacters.Clear();

        if (selectedCharacters == null)
            return;

        for (int i = 0; i < selectedCharacters.Count; i++)
            onlineSelectedCharacters.Add(selectedCharacters[i]);

        Debug.Log($"[PlayerInputService] Stored {onlineSelectedCharacters.Count} online character selections.");
    }

    // ======================================================
    //  GAMEPLAY PLAYER SPAWNING (PlayerManager)
    // ======================================================
    public PlayerInput SpawnGameplayPlayer(int index, GameObject charPrefab, string scheme, InputDevice dev)
    {
        Debug.Log($"[PlayerInputService] Spawning index={index} requestedScheme={scheme} requestedDevice={(dev != null ? $"{dev.displayName} ({dev.path})" : "NULL")}");

        PlayerInput pi = PlayerInput.Instantiate(
            charPrefab,
            index,
            scheme,
            -1,
            dev
        );

        Debug.Log($"[PlayerInputService] Spawned index={index} -> pi.playerIndex={pi.playerIndex} currentScheme={pi.currentControlScheme} devices=[{string.Join(", ", pi.devices)}]");

        // Insert exactly in its slot
        if (index >= players.Count)
            players.Add(pi);
        else
            players.Insert(index, pi);

        return pi;
    }

}
