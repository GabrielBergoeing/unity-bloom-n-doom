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

    private void Start()
    {
        Debug.Log("[SettingsMenu] Start → Listening for tab change");
        tabMenu.OnPageIndexChanged.AddListener(TabChanged);
    }

    private void OnDestroy()
    {
        Debug.Log("[SettingsMenu] Destroy → Removing listener");
        tabMenu.OnPageIndexChanged.RemoveListener(TabChanged);
    }

    #region Control Management
    private void TabChanged(int index)
    {
        Debug.Log($"[SettingsMenu] Tab changed → {index}");
        UIService.instance.sfx.PlayOnToggle();
        if (index == controlsPageIndex)
        {
            Debug.Log("[SettingsMenu] Controls tab opened → regenerating controls");
            RegenerateControlsImmediate();
        }
    }

    public void RefreshIfActiveAndOnControlsTab()
    {
        Debug.Log($"[SettingsMenu] Refresh check | active={gameObject.activeInHierarchy}, onControls={IsControlsTabOpen}");
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
        {
            Debug.LogWarning("[SettingsMenu] Regeneration blocked (already regenerating)");
            return;
        }

        Debug.Log("[SettingsMenu] Regenerating Control Bindings");
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
        Debug.Log("[SettingsMenu] Generating bindings...");
        var svc = PlayerInputService.instance;
        var cfgs = svc.Configs;

        if (cfgs.Count > 0)
        {
            for (int i = 0; i < cfgs.Count; i++)
            {
                var player = svc.GetPlayerByDevice(cfgs[i].device);
                Debug.Log($"[SettingsMenu] Checking player config index={i} device={cfgs[i].device}");

                var map = player?.actions.FindActionMap("Player") ??
                          inputActions.FindActionMap("Player");

                if (map != null)
                {
                    Debug.Log($"[SettingsMenu] Generating map for player {i} → {map.name}");
                    inputFactory.Generate(map, i);
                }
                else
                {
                    Debug.LogError($"[SettingsMenu] Could NOT find 'Player' Action Map for index={i}");
                }
            }
        }
        else
        {
            Debug.Log("[SettingsMenu] No player configs → default map");
            var map = inputActions.FindActionMap("Player");
            if (map != null)
            {
                Debug.Log($"[SettingsMenu] Default map → {map.name}");
                inputFactory.Generate(map, 0);
            }
        }
    }

    private void RestoreSelection()
    {
        StartCoroutine(RestoreSelectionDelayed());
    }

    private IEnumerator RestoreSelectionDelayed()
    {
        // wait one frame so the factory can fully populate
        yield return null; 

        if (!IsControlsTabOpen) yield break;
        if (EventSystem.current == null) yield break;

        var first = tabMenu.GetCurrentPageObject()?.GetComponentInChildren<Selectable>(true);

        if (first != null)
        {
            Debug.Log($"[SettingsMenu] Selecting {first.gameObject.name}");
            EventSystem.current.SetSelectedGameObject(first.gameObject);
        }
    }
    #endregion

    #region Buttons
    public void ReturnBTN()
    {
        Debug.Log("[SettingsMenu] Returning to main menu...");
        UIService.instance.sfx.PlayOnToggle();
        UIService.instance.menu.HideSettingsOverlay();

        var ev = EventSystem.current;
        var menuObj = UIService.instance.menu.mainMenuPanel;
        var selectable = menuObj.GetComponentInChildren<Selectable>(true);

        if (selectable != null)
        {
            Debug.Log($"[SettingsMenu] Selected Main Menu UI Element → {selectable.gameObject.name}");
            ev?.SetSelectedGameObject(selectable.gameObject);
        }
    }
    #endregion
}
