using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

public class UI_InputPrompt : MonoBehaviour
{
    private InputAction action;
    private InputBinding binding;
    private GamepadIconsDatabase database;
    private int bindingIndex = -1;

    private PlayerInput player;
    private int playerIndex;

    [Header("UI")]
    public TextMeshProUGUI actionLabel;
    public Image iconImage;

    // public so factory can wire button click
    public void Setup(InputAction action, InputBinding binding, GamepadIconsDatabase db, int playerIdx)
    {
        this.action = action;
        this.binding = binding;
        this.database = db;
        this.playerIndex = playerIdx;
        this.bindingIndex = FindBindingIndex(action, binding);

        // If no real player exists for this index, set null
        player = (playerIdx < PlayerInput.all.Count) ? PlayerInput.all[playerIdx] : null;

        if (actionLabel != null) actionLabel.text = action?.name ?? "Unknown";

        UpdateIcon();

        InputSystem.onActionChange += OnActionChange;
        InputSystem.onDeviceChange += OnDeviceChange;

        if (player != null)
            player.onControlsChanged += OnControlsChanged;
    }

    // public so factory button can call it
    public void StartRebind()
    {
        if (action == null)
        {
            Debug.LogWarning("[UI_InputPrompt] StartRebind called but action is null");
            return;
        }

        if (bindingIndex < 0)
        {
            Debug.LogWarning($"[UI_InputPrompt] StartRebind: bindingIndex invalid for action {action.name}");
            return;
        }

        Debug.Log($"[UI_InputPrompt] Start rebind '{action.name}' bindingIndex {bindingIndex} (path {binding.effectivePath})");

        action.Disable();

        // Create rebind operation for the exact binding index
        action.PerformInteractiveRebinding(bindingIndex)
            .WithControlsExcluding("Mouse") // example: exclude mouse if you want
            .OnComplete(op =>
            {
                action.Enable();
                Debug.Log($"[UI_InputPrompt] Rebind complete: {action.bindings[bindingIndex].effectivePath}");
                op.Dispose();
                // update internal 'binding' reference to latest binding
                binding = action.bindings[bindingIndex];
                UpdateIcon();
            })
            .Start();
    }

    private void OnDestroy()
    {
        InputSystem.onActionChange -= OnActionChange;
        InputSystem.onDeviceChange -= OnDeviceChange;
        if (player != null) player.onControlsChanged -= OnControlsChanged;
    }

    private void OnControlsChanged(PlayerInput p)
    {
        if (p == player) UpdateIcon();
    }

    private void OnDeviceChange(InputDevice dev, InputDeviceChange change)
    {
        if (player != null && player.OwnsDevice(dev)) UpdateIcon();
    }

    private void OnActionChange(object obj, InputActionChange change)
    {
        if (obj == action && change == InputActionChange.BoundControlsChanged) UpdateIcon();
    }

    private void UpdateIcon()
    {
        if (database == null || binding.effectivePath == null)
            return;

        // ---------------------------------------------------------
        //      FIX: Proper device resolution for UI screens
        // ---------------------------------------------------------
        InputDevice targetDevice = null;

        // Case 1: We have a real PlayerInput (in gameplay)
        if (player != null)
        {
            // Prefer last-used device
            targetDevice = PlayerInputService.instance != null 
                ? PlayerInputService.instance.LastUsedDevice 
                : null;

            // Fallback: player's actual devices
            if (targetDevice == null && player.devices.Count > 0)
                targetDevice = player.devices[0];
        }
        else
        {
            // Case 2: No PlayerInput (menus) → use global detection
            if (PlayerInputService.instance != null)
                targetDevice = PlayerInputService.instance.GetActiveDevice();
        }

        // Final fallback: Still null? Use keyboard if available
        if (targetDevice == null)
        {
            if (Keyboard.current != null) targetDevice = Keyboard.current;
            else if (Gamepad.current != null) targetDevice = Gamepad.current;
        }

        // ---------------------------------------------------------
        //      Safety: If device still null, show generic icons
        // ---------------------------------------------------------
        string deviceType = "Generic";
        if (targetDevice != null)
        {
            if (targetDevice is Gamepad)
            {
                string layout = (targetDevice.layout ?? "").ToLower();
                if (layout.Contains("xinput")) deviceType = "XboxController";
                else if (layout.Contains("dualshock") || layout.Contains("dualsense") || layout.Contains("ps")) deviceType = "PlayStation";
                else if (layout.Contains("switch")) deviceType = "Switch";
                else deviceType = "Gamepad";
            }
            else if (targetDevice is Keyboard)
            {
                deviceType = "Keyboard";
            }
        }

        // ---------------------------------------------------------
        //           DEBUG OUTPUT (requested)
        // ---------------------------------------------------------
        Debug.Log($"[UI_InputPrompt] Device Debug → " +
            $"targetDevice: {(targetDevice != null ? targetDevice.displayName : "NULL")}, " +
            $"layout: {(targetDevice != null ? targetDevice.layout : "NULL")}, " +
            $"type selected: {deviceType}, " +
            $"binding: {binding.effectivePath}, action: {action?.name}");

        // ---------------------------------------------------------
        //            ICON RESOLUTION
        // ---------------------------------------------------------
        string buttonName = ExtractName(binding.effectivePath);
        Sprite icon = database.GetIcon(deviceType, buttonName);

        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.enabled = icon != null;
        }
    }


    private string ExtractName(string path)
    {
        if (string.IsNullOrEmpty(path)) return "";
        int i = path.LastIndexOf('/');
        return i >= 0 ? path.Substring(i + 1) : path;
    }

    private int FindBindingIndex(InputAction action, InputBinding target)
    {
        var bindings = action.bindings;
        for (int i = 0; i < bindings.Count; i++)
        {
            var b = bindings[i];
            if (b.effectivePath == target.effectivePath &&
                b.action == target.action &&
                b.groups == target.groups)
            {
                return i;
            }
        }

        Debug.LogWarning($"[UI_InputPrompt] Could not find binding index for {target.effectivePath} on action {action?.name}");
        return -1;
    }
}
