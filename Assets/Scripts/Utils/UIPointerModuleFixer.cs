using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

// Several menu scenes' InputSystemUIInputModule never had a Point/LeftClick action wired
// up in the Editor, so mouse hover/click on UI buttons silently did nothing there (Navigate/
// Submit/Cancel still worked fine, since those were already assigned) - and OnlineLobby.unity
// has its whole actionsAsset reference broken (dangling guid, module.actionsAsset resolves
// to null), so nothing there works at all. Hand-editing each scene's serialized action
// references to fix this is exactly the kind of edit that broke OnlineLobby.unity in the
// first place, so this fills in whatever's missing from code instead, covering every scene
// (present and future) without touching any .unity file. Each InputActionReference below is
// independent of module.actionsAsset, so this repairs OnlineLobby too even though its own
// actionsAsset stays broken.
public static class UIPointerModuleFixer
{
    private static InputActionAsset uiActions;

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

        if (uiActions == null)
            uiActions = Resources.Load<InputActionAsset>("PlayerInputSystem");

        if (uiActions == null)
        {
            Debug.LogWarning("[UIPointerModuleFixer] Could not load PlayerInputSystem actions from Resources.");
            return;
        }

        AssignIfMissing(module, m => m.point, (m, r) => m.point = r, "UI/Point");
        AssignIfMissing(module, m => m.leftClick, (m, r) => m.leftClick = r, "UI/Click");
        AssignIfMissing(module, m => m.move, (m, r) => m.move = r, "UI/Navigate");
        AssignIfMissing(module, m => m.submit, (m, r) => m.submit = r, "UI/Submit");
        AssignIfMissing(module, m => m.cancel, (m, r) => m.cancel = r, "UI/Cancel");
    }

    private static void AssignIfMissing(
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
