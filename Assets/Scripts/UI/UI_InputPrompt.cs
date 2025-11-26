using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

public class UI_InputPrompt : MonoBehaviour
{
    private InputAction action;
    private InputBinding binding;
    private GamepadIconsDatabase database;
    private PlayerInput player;
    private int bindingIndex = -1;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI actionLabel;
    [SerializeField] private Image iconImage;

    public void Setup(InputAction action, InputBinding binding, GamepadIconsDatabase db, int playerIndex)
    {
        this.action = action;
        this.binding = binding;
        this.database = db;

        player = (playerIndex < PlayerInput.all.Count) ? PlayerInput.all[playerIndex] : null;
        bindingIndex = FindBindingIndex(action, binding);

        if (actionLabel != null)
            actionLabel.text = action?.name ?? "Unknown";

        gameObject.name = $"Rebind_{action.name}_{UIDeviceResolver.ExtractButtonName(binding.effectivePath)}";

        SubscribeEvents();
        UpdateIcon();
    }

    private void SubscribeEvents()
    {
        InputSystem.onActionChange += HandleActionChange;
        InputSystem.onDeviceChange += HandleDeviceChange;
        if (player != null) player.onControlsChanged += HandlePlayerControlsChanged;
    }

    private void OnDestroy()
    {
        InputSystem.onActionChange -= HandleActionChange;
        InputSystem.onDeviceChange -= HandleDeviceChange;
        if (player != null) player.onControlsChanged -= HandlePlayerControlsChanged;
    }

    public void StartRebind()
    {
        if (action == null || bindingIndex < 0) return;

        action.Disable();
        action.PerformInteractiveRebinding(bindingIndex)
            .WithControlsExcluding("Mouse")
            .OnComplete(op =>
            {
                action.Enable();
                binding = action.bindings[bindingIndex];
                op.Dispose();
                UpdateIcon();
            })
            .Start();
    }

    private void UpdateIcon()
    {
        if (database == null || binding.effectivePath == null) return;

        var device = UIDeviceResolver.ResolveActiveDevice(player);
        string deviceType = UIDeviceResolver.ResolveDeviceType(device);
        string buttonName = UIDeviceResolver.ExtractButtonName(binding.effectivePath);

        Sprite icon = database.GetIcon(deviceType, buttonName);

        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.enabled = icon != null;
        }
    }

    // ---------- Event Reactors ----------
    private void HandlePlayerControlsChanged(PlayerInput p) { if (p == player) UpdateIcon(); }
    private void HandleDeviceChange(InputDevice dev, InputDeviceChange c) { UpdateIcon(); }
    private void HandleActionChange(object obj, InputActionChange change)
    {
        if (obj == action && change == InputActionChange.BoundControlsChanged)
            UpdateIcon();
    }

    private int FindBindingIndex(InputAction action, InputBinding target)
    {
        for (int i = 0; i < action.bindings.Count; i++)
            if (action.bindings[i].effectivePath == target.effectivePath)
                return i;
        return -1;
    }

    // ---------- Sounds ----------
    public void PlayToggleSFX()
    {
        UIService.instance.sfx.PlayOnToggle();
    }

    public void PlayHoverSFX()
    {
        UIService.instance.sfx.PlayOnHover();
    }
}
