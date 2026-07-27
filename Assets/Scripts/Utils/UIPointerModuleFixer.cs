using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

// Several menu scenes' InputSystemUIInputModule never had a Point/Click action wired up
// in the Editor, so mouse hover/click on UI buttons silently did nothing there (Navigate/
// Submit/Cancel still worked fine, since those were already assigned). Hand-editing each
// scene's serialized action references to fix this risks breaking scenes the same way a
// stray edit could, so this fills in whatever's missing from code instead, covering every
// scene (present and future) without touching any .unity file. Reads the actions straight
// off the module's own actionsAsset (whatever scene wired it up with) rather than a fixed
// Resources path, so it works regardless of where PlayerInputSystem.inputactions lives.
public static class UIPointerModuleFixer
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Init()
    {
        SceneManager.sceneLoaded += (_, __) => FixCurrentScene();
    }

    private static void FixCurrentScene()
    {
        InputSystemUIInputModule module = EventSystem.current != null
            ? EventSystem.current.GetComponent<InputSystemUIInputModule>()
            : null;

        if (module == null)
            return;

        bool needsFix = module.point == null || module.leftClick == null
            || module.move == null || module.submit == null || module.cancel == null;

        if (!needsFix)
            return;

        InputActionAsset uiActions = module.actionsAsset;
        if (uiActions == null)
        {
            Debug.LogWarning("[UIPointerModuleFixer] InputSystemUIInputModule has no actionsAsset assigned.");
            return;
        }

        AssignIfMissing(uiActions, module, m => m.point, (m, r) => m.point = r, "UI/Point");
        AssignIfMissing(uiActions, module, m => m.leftClick, (m, r) => m.leftClick = r, "UI/Click");
        AssignIfMissing(uiActions, module, m => m.move, (m, r) => m.move = r, "UI/Navigate");
        AssignIfMissing(uiActions, module, m => m.submit, (m, r) => m.submit = r, "UI/Submit");
        AssignIfMissing(uiActions, module, m => m.cancel, (m, r) => m.cancel = r, "UI/Cancel");
    }

    private static void AssignIfMissing(
        InputActionAsset uiActions,
        InputSystemUIInputModule module,
        System.Func<InputSystemUIInputModule, InputActionReference> get,
        System.Action<InputSystemUIInputModule, InputActionReference> set,
        string actionPath)
    {
        if (get(module) != null)
            return;

        InputAction action = uiActions.FindAction(actionPath);
        if (action != null)
            set(module, InputActionReference.Create(action));
    }
}
