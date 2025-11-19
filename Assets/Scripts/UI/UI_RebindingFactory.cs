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

    private List<UI_InputPrompt> generatedEntries = new();

    public void Generate(InputActionMap map, int playerIndex)
    {
        Clear();

        if (map == null)
        {
            Debug.LogWarning("[UI_InputFactory] map is null");
            return;
        }

        InputDevice device = PlayerInputService.instance != null
            ? PlayerInputService.instance.GetActiveDevice()
            : null;

        Debug.Log($"[UI_InputFactory] Generating for map '{map.name}' playerIndex {playerIndex} device {(device != null ? device.displayName : "NULL")}");

        foreach (var action in map.actions)
        {
            foreach (var binding in action.bindings)
            {
                // ignore composites AND composite parts
                if (binding.isComposite || binding.isPartOfComposite)
                    continue;

                if (!BindingMatchesDevice(binding, device)) 
                    continue;

                GameObject entryObj = Instantiate(inputEntryPrefab);
                entryObj.transform.SetParent(contentParent, false);

                var rt = entryObj.GetComponent<RectTransform>();
                if (rt != null) rt.localScale = Vector3.one;

                UI_InputPrompt ui = entryObj.GetComponent<UI_InputPrompt>();
                if (ui == null)
                {
                    Debug.LogError("[UI_InputFactory] prefab missing UI_InputPrompt!");
                    Destroy(entryObj);
                    continue;
                }

                ui.Setup(action, binding, iconsDatabase, playerIndex);

                var btn = entryObj.GetComponentInChildren<UnityEngine.UI.Button>(true);
                if (btn != null)
                    btn.onClick.AddListener(() => ui.StartRebind());

                generatedEntries.Add(ui);
            }
        }
    }

    public void Clear()
    {
        foreach (var ui in generatedEntries)
            if (ui != null) Destroy(ui.gameObject);

        generatedEntries.Clear();
    }

    public static bool BindingMatchesDevice(InputBinding binding, InputDevice device)
    {
        // if no device detected, we allow everything (fallback) - the factory will show generic icons
        if (device == null) return true;

        string path = (binding.effectivePath ?? "").ToLower();

        // direct prefixes used by the Input System are reliable
        if (path.StartsWith("<keyboard>")) return device is Keyboard;
        if (path.StartsWith("<mouse>")) return device is Mouse;
        if (path.StartsWith("<gamepad>")) return device is Gamepad;

        // fallback: check for common substrings
        string layout = (device.layout ?? "").ToLower();
        if (!string.IsNullOrEmpty(layout) && path.Contains(layout)) return true;

        // last resort: accept (so user sees something) — but usually should be filtered
        return false;
    }
}
