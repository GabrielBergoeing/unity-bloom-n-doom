using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class UI_InputFactory : MonoBehaviour
{
    [SerializeField] private GameObject inputEntryPrefab;
    [SerializeField] private Transform contentParent;
    [SerializeField] private GamepadIconsDatabase iconsDatabase;

    private readonly List<UI_InputPrompt> generated = new();

    public void Generate(InputActionMap map, int playerIndex)
    {
        if (map == null) return;

        var device = PlayerInputService.instance?.GetActiveDevice();
        foreach (var action in map.actions)
            foreach (var binding in action.bindings)
                if (!binding.isComposite && !binding.isPartOfComposite && MatchDevice(binding, device))
                    CreatePrompt(action, binding, playerIndex);
    }

    private void CreatePrompt(InputAction action, InputBinding binding, int index)
    {
        var obj = Instantiate(inputEntryPrefab, contentParent);
        var ui = obj.GetComponent<UI_InputPrompt>();
        ui?.Setup(action, binding, iconsDatabase, index);
        generated.Add(ui);
    }

    public void Clear()
    {
        foreach (var ui in generated)
            if (ui != null) Destroy(ui.gameObject);
        generated.Clear();
    }

    private static bool MatchDevice(InputBinding binding, InputDevice device)
    {
        if (device == null) return true;
        string path = (binding.effectivePath ?? "").ToLower();
        if (path.StartsWith("<keyboard>")) return device is Keyboard;
        if (path.StartsWith("<mouse>")) return device is Mouse;
        if (path.StartsWith("<gamepad>")) return device is Gamepad;
        return false;
    }
}
