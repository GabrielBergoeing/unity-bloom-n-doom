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

    [Header("Tabs & Pages")]
    [SerializeField] private ToggleGroup toggleGroup;
    [SerializeField] private List<Toggle> tabs = new();
    [SerializeField] private List<CanvasGroup> pages = new();

    [Tooltip("Starting page for this menu")]
    [SerializeField] private int startingPage = 0;
    private bool initialized = false;
    
    public UnityEvent<int> OnPageIndexChanged;

    #region LIFECYCLE
    private void Awake() => CacheUI();
    private void Start() => InitRuntime();
    private void OnDestroy() => RemoveListeners();
    private void OnValidate() { if (!Application.isPlaying) CacheUI(); }
    private void Reset() => CacheUI();
    #endregion

    #region INITIALIZATION
    private void CacheUI()
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
                if (child.TryGetComponent(out CanvasGroup cg))
                    pages.Add(cg);
        }
    }

    private void InitRuntime()
    {
        if (initialized) return;
        initialized = true;

        SetupListeners();

        // Only apply once, not every enable.
        pageIndex = startingPage;
        JumpToPage(pageIndex);
    }

    private void SetupListeners()
    {
        foreach (var toggle in tabs)
        {
            toggle.onValueChanged.RemoveListener(OnTabChanged);
            toggle.onValueChanged.AddListener(OnTabChanged);
            toggle.group = toggleGroup;
        }
    }

    private void RemoveListeners()
    {
        foreach (var toggle in tabs)
            toggle.onValueChanged.RemoveListener(OnTabChanged);
    }
    #endregion

    #region TAB LOGIC
    private void OnTabChanged(bool value)
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

    private void OpenPage(int index)
    {
        EnsureRange(index);

        for (int i = 0; i < pages.Count; i++)
        {
            bool active = (i == pageIndex);
            pages[i].alpha = active ? 1f : 0f;
            pages[i].interactable = active;
            pages[i].blocksRaycasts = active;
        }

        OnPageIndexChanged?.Invoke(pageIndex);
        SelectFirstElement();
    }

    private void SelectFirstElement()
    {
        if (EventSystem.current == null) return;

        foreach (var selectable in pages[pageIndex].GetComponentsInChildren<Selectable>(true))
        {
            // Ignore sliders when auto-selecting
            if (selectable is Slider) 
                continue;

            EventSystem.current.SetSelectedGameObject(selectable.gameObject);
            return;
        }
    }

    private void EnsureRange(int index)
    {
        pageIndex = Mathf.Clamp(index, 0, pages.Count - 1);
    }

    public void JumpToPage(int index)
    {
        EnsureRange(index);
        tabs[pageIndex].SetIsOnWithoutNotify(true);
        OpenPage(pageIndex);
    }
    #endregion

    public GameObject GetCurrentPageObject() =>
        (pageIndex >= 0 && pageIndex < pages.Count) ? pages[pageIndex].gameObject : null;
}
