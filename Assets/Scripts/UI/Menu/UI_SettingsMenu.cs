using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UI_SettingsMenu : MonoBehaviour
{
    #region Variables
    [Header("Wiring")]
    [SerializeField] private UI_InputFactory inputFactory;
    [SerializeField] private UI_TabMenu tabMenu;
    [SerializeField] private InputActionAsset inputActions;
    [Tooltip("Index of the 'Controls' tab inside the TabMenu")]
    public int controlsPageIndex = 1;

    private bool isRegenerating;

    public bool IsControlsTabOpen =>
        tabMenu != null && tabMenu.CurrentPageIndex == controlsPageIndex;
    #endregion

    private void Start() => tabMenu.OnPageIndexChanged.AddListener(TabChanged);

    private void OnDestroy() => tabMenu.OnPageIndexChanged.RemoveListener(TabChanged);

    #region Control Management
    private void TabChanged(int index)
    {
        UIService.instance.sfx.PlayOnToggle();
        if (index == controlsPageIndex)
            RegenerateControlsImmediate();
    }

    public void RefreshIfActiveAndOnControlsTab()
    {
        if (gameObject.activeInHierarchy && IsControlsTabOpen)
            StartCoroutine(DelayedRefresh());
    }

    private IEnumerator DelayedRefresh()
    {
        yield return null;
        RegenerateControlsImmediate();
    }

    public void RegenerateControlsImmediate()
    {
        if (isRegenerating)
            return;

        isRegenerating = true;

        inputFactory.Clear();
        GenerateBindings();
        RestoreSelection();

        isRegenerating = false;
    }
    #endregion

    #region Bindings Rebinding
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

                if (map != null)
                    inputFactory.Generate(map, i);
                else
                    Debug.LogError($"[SettingsMenu] Could NOT find 'Player' Action Map for index={i}");
            }
        }
        else
        {
            var map = inputActions.FindActionMap("Player");
            if (map != null)
                inputFactory.Generate(map, 0);
        }
    }

    private void RestoreSelection()
    {
        StartCoroutine(RestoreSelectionDelayed());
    }

    private IEnumerator RestoreSelectionDelayed()
    {
        // wait one frame so the factory can fully populate
        yield return new WaitForEndOfFrame(); 

        if (!IsControlsTabOpen) yield break;
        if (EventSystem.current == null) yield break;

        var first = tabMenu.GetCurrentPageObject()?.GetComponentInChildren<Selectable>(true);

        if (first != null)
            EventSystem.current.SetSelectedGameObject(first.gameObject);
    }
    #endregion

    #region Buttons
    public void ReturnBTN()
    {
        UIService.instance.sfx.PlayOnToggle();
        UIService.instance.menu.HideSettingsOverlay();

        var ev = EventSystem.current;
        var menuObj = UIService.instance.menu.mainMenuPanel;
        var selectable = menuObj.GetComponentInChildren<Selectable>(true);

        if (selectable != null)
            ev?.SetSelectedGameObject(selectable.gameObject);
    }
    #endregion
}
