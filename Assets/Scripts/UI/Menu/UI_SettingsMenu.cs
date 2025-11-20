using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UI_SettingsMenu : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] private UI_InputFactory inputFactory;
    [SerializeField] private UI_TabMenu tabMenu;
    [SerializeField] private InputActionAsset inputActions;
    [Tooltip("Index of the 'Controls' tab inside the TabMenu")]
    public int controlsPageIndex = 1;

    private bool isRegenerating;

    public bool IsControlsTabOpen =>
        tabMenu != null && tabMenu.CurrentPageIndex == controlsPageIndex;

    private void Start() => tabMenu.OnPageIndexChanged.AddListener(TabChanged);
    private void OnDestroy() => tabMenu.OnPageIndexChanged.RemoveListener(TabChanged);

    private void TabChanged(int index)
    {
        if (index == controlsPageIndex)
            RegenerateControlsImmediate();
    }

    public void RefreshIfActiveAndOnControlsTab()
    {
        if (gameObject.activeInHierarchy && IsControlsTabOpen)
            RegenerateControlsImmediate();
    }

    public void RegenerateControlsImmediate()
    {
        if (isRegenerating) return;
        isRegenerating = true;

        inputFactory.Clear();
        GenerateBindings();
        RestoreSelection();

        isRegenerating = false;
    }

    private void GenerateBindings()
    {
        var svc = PlayerInputService.instance;
        var cfgs = svc.Configs;

        if (cfgs.Count > 0)
        {
            for (int i = 0; i < cfgs.Count; i++)
            {
                var player = svc.GetPlayerByDevice(cfgs[i].device);
                var map = player?.actions.FindActionMap("Player") ??
                          inputActions.FindActionMap("Player");
                if (map != null) inputFactory.Generate(map, i);
            }
        }
        else
        {
            var map = inputActions.FindActionMap("Player");
            if (map != null) inputFactory.Generate(map, 0);
        }
    }

    private void RestoreSelection()
    {
        if (!IsControlsTabOpen) return;
        if (EventSystem.current == null) return;

        var first = tabMenu.GetCurrentPageObject()?.GetComponentInChildren<Selectable>(true);
        if (first != null) EventSystem.current.SetSelectedGameObject(first.gameObject);
    }

    public void ReturnBTN()
    {
        UIService.instance.sfx.PlayOnConfirm();
        UIService.instance.menu.HideSettingsOverlay();

        var ev = EventSystem.current;
        var menuObj = UIService.instance.menu.mainMenuPanel;
        var selectable = menuObj.GetComponentInChildren<Selectable>(true);
        if (selectable != null) ev?.SetSelectedGameObject(selectable.gameObject);
    }
}

