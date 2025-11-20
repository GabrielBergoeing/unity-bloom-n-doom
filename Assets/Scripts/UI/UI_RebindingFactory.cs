using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class UI_InputFactory : MonoBehaviour
{
    [Header("Prefab (ActionName + Icon)")]
    public GameObject inputEntryPrefab;

    [Header("Target container for entries")]
    public Transform contentParent;

    [Header("Database")]
    public GamepadIconsDatabase iconsDatabase;

    private readonly List<UI_InputPrompt> generatedEntries = new();

    public void Generate(InputActionMap map, int playerIndex)
    {
        if (map == null)
        {
            Debug.LogWarning("[UI_InputFactory] NULL map");
            return;
        }

        var device = PlayerInputService.instance?.GetActiveDevice();
        Debug.Log($"[UI_InputFactory] Generating for '{map.name}' device {device?.displayName ?? "NULL"}");

        foreach (var action in map.actions)
        {
            foreach (var binding in action.bindings)
            {
                if (binding.isComposite || binding.isPartOfComposite) continue;
                if (!BindingMatchesDevice(binding, device)) continue;

                CreatePrompt(action, binding, playerIndex);
            }
        }
    }

    private void CreatePrompt(InputAction action, InputBinding binding, int index)
    {
        var obj = Instantiate(inputEntryPrefab, contentParent);
        var ui = obj.GetComponent<UI_InputPrompt>();

        if (ui == null)
        {
            Debug.LogError("[UI_InputFactory] prefab missing UI_InputPrompt!");
            Destroy(obj);
            return;
        }

        ui.Setup(action, binding, iconsDatabase, index);

        var btn = obj.GetComponentInChildren<UnityEngine.UI.Button>(true);
        btn?.onClick.AddListener(() => ui.StartRebind());

        generatedEntries.Add(ui);
    }

    public void Clear()
    {
        foreach (var ui in generatedEntries)
            if (ui != null) Destroy(ui.gameObject);

        generatedEntries.Clear();
    }

    public static bool BindingMatchesDevice(InputBinding binding, InputDevice device)
    {
        if (device == null) return true;
        string path = (binding.effectivePath ?? "").ToLower();

        if (path.StartsWith("<keyboard>")) return device is Keyboard;
        if (path.StartsWith("<mouse>")) return device is Mouse;
        if (path.StartsWith("<gamepad>")) return device is Gamepad;

        return false;
    }
}

