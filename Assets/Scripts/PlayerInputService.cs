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
        var settings = FindObjectOfType<UI_SettingsMenu>(true);
        if (settings == null || !settings.gameObject.activeInHierarchy)
            return;

        // Only refresh IF controls tab is open
        if (settings.IsControlsTabOpen)
            settings.RegenerateControlsImmediate();

        RefreshUIInputModule();
    }

    private void RefreshUIInputModule()
    {
        var uiModule = FindObjectOfType<UnityEngine.InputSystem.UI.InputSystemUIInputModule>(true);
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
        // prefer the device that triggered the change (if any)
        if (pi.devices.Count > 0)
        {
            LastUsedDevice = pi.devices[0];
        }
    }

    // ======================================================
    //  CONFIG MANAGEMENT (used by UI_MatchMenu)
    // ======================================================
    public void StoreLobbyPlayers(PlayerInput[] lobbyPlayers)
    {
        configs.Clear();

        foreach (var p in lobbyPlayers)
        {
            if (p.devices.Count == 0) continue;

            configs.Add(new PlayerConfiguration
            {
                device = p.devices[0],
                controlScheme = p.currentControlScheme
            });

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

    // ======================================================
    //  GAMEPLAY PLAYER SPAWNING (PlayerManager)
    // ======================================================
    public PlayerInput SpawnGameplayPlayer(int index, GameObject charPrefab, string scheme, InputDevice dev)
    {
        PlayerInput pi = PlayerInput.Instantiate(
            charPrefab,
            index,
            scheme,
            -1,
            dev
        );

        players.Add(pi);
        return pi;
    }
}
