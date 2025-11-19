using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UI_TabMenu : MonoBehaviour
{
    [Header("Current Index")]
    [SerializeField] private int pageIndex = 0;
    public int CurrentPageIndex => pageIndex;

    [Header("Components")]
    [SerializeField] private ToggleGroup toggleGroup;
    [SerializeField] private List<Toggle> tabs = new List<Toggle>();
    [SerializeField] private List<CanvasGroup> pages = new List<CanvasGroup>();

    [Header("Event to call")]
    public UnityEvent<int> OnPageIndexChanged;

    private bool initialized = false;

    // ---------------------------------------------------------
    // INITIALIZATION (safe for runtime AND editor)
    // ---------------------------------------------------------
    private void Initialize()
    {
        tabs.Clear();
        pages.Clear();

        toggleGroup = GetComponentInChildren<ToggleGroup>(true);

        var tabsParent = transform.Find("Tabs");
        if (tabsParent != null)
            tabs.AddRange(tabsParent.GetComponentsInChildren<Toggle>(true));

        var pagesParent = transform.Find("Pages");
        if (pagesParent != null)
        {
            foreach (Transform child in pagesParent)
            {
                var cg = child.GetComponent<CanvasGroup>();
                if (cg != null)
                    pages.Add(cg);
            }
        }

        initialized = true;
        SetupToggleListeners();
    }

    private void SetupToggleListeners()
    {
        foreach (var toggle in tabs)
        {
            toggle.onValueChanged.RemoveListener(CheckForTab);
            toggle.onValueChanged.AddListener(CheckForTab);
            toggle.group = toggleGroup;
        }
    }

    private void Reset()
    {
        Initialize();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
            Initialize();
    }

    private void Awake()
    {
        Initialize(); // ensure everything is loaded BEFORE Start
    }

    private void Start()
    {
        // Auto-select the first tab
        if (tabs.Count > 0)
        {
            tabs[0].SetIsOnWithoutNotify(true);
            OpenPage(0);
        }
    }

    private void OnDestroy()
    {
        foreach (var toggle in tabs)
            toggle.onValueChanged.RemoveListener(CheckForTab);
    }

    // ---------------------------------------------------------
    // TAB SELECTION
    // ---------------------------------------------------------
    private void CheckForTab(bool value)
    {
        if (!value) return;

        for (int i = 0; i < tabs.Count; i++)
        {
            if (tabs[i].isOn)
            {
                pageIndex = i;
                break;
            }
        }

        OpenPage(pageIndex);
    }

    // ---------------------------------------------------------
    // PAGE SWITCHING
    // ---------------------------------------------------------
    private void OpenPage(int index)
    {
        EnsureIndexIsInRange(index);

        for (int i = 0; i < pages.Count; i++)
        {
            bool isActive = (i == pageIndex);
            pages[i].alpha = isActive ? 1 : 0;
            pages[i].interactable = isActive;
            pages[i].blocksRaycasts = isActive;
        }

        if (Application.isPlaying)
            OnPageIndexChanged?.Invoke(pageIndex);

        SelectFirstUIElement();
    }

    private void SelectFirstUIElement()
    {
        var activePage = pages[pageIndex].gameObject;
        var firstSel = activePage.GetComponentInChildren<Selectable>();
        if (firstSel != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(firstSel.gameObject);
    }

    private void EnsureIndexIsInRange(int index)
    {
        if (tabs.Count == 0 || pages.Count == 0)
        {
            Debug.LogWarning("[UI_TabMenu] Tabs or Pages missing!");
            return;
        }

        pageIndex = Mathf.Clamp(index, 0, pages.Count - 1);
    }

    // ---------------------------------------------------------
    // PUBLIC API
    // ---------------------------------------------------------
    public void JumpToPage(int page)
    {
        EnsureIndexIsInRange(page);
        tabs[pageIndex].isOn = true;
    }

    /// <summary>
    /// Called by SettingsMenu when input device swaps.
    /// Ensures pages and tabs resync.
    /// </summary>
    public void ReinitializeTabs()
    {
        Initialize();
        OpenPage(pageIndex);
    }
}
