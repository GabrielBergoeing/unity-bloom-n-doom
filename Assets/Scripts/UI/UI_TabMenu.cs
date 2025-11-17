using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UI_TabMenu : MonoBehaviour
{

    [Header("Current Index")]
    [SerializeField] private int pageIndex = 0;

    [Header("Components")]
    [SerializeField] private ToggleGroup toggleGroup;
    [SerializeField] private List<Toggle> tabs = new List<Toggle>();
    [SerializeField] private List<CanvasGroup> pages = new List<CanvasGroup>();

    [Header("Event to call")]
    public UnityEvent<int> OnPageIndexChanged;

    private void Initialize()
    {
        toggleGroup = GetComponentInChildren<ToggleGroup>();

        tabs.Clear();
        pages.Clear();

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
    }


    private void Reset()
    {
        Initialize();
    }
    private void OnValidate()
    {
        Initialize();

        if (tabs == null || pages == null) return;
        if (tabs.Count == 0 || pages.Count == 0) return;
        if (pageIndex < 0 || pageIndex >= pages.Count) return;

        // Evita nulls si se ejecuta mientras el editor actualiza la jerarquía
        if (tabs[pageIndex] == null || pages[pageIndex] == null)
            return;

        OpenPage(pageIndex);

        // Evita null si la lista de tabs cambia durante validación
        if (pageIndex < tabs.Count && tabs[pageIndex] != null)
            tabs[pageIndex].SetIsOnWithoutNotify(true);
    }

    private void Awake()
    {
        foreach (var toggle in tabs)
        {
            toggle.onValueChanged.AddListener(CheckForTab);
            toggle.group = toggleGroup;
        }
    }

    private void OnDestroy()
    {
        foreach (var toggle in tabs)
        {
            toggle.onValueChanged.RemoveListener(CheckForTab);
        }
    }
    private void CheckForTab(bool value)
    {
        for (int i = 0; i < tabs.Count; i++)
        {
            if (!tabs[i].isOn) continue;
            pageIndex = i;
        }

        OpenPage(pageIndex);
    }

    private void OpenPage(int index)
    {
        EnsureIndexIsInRange(index);

        for (int i = 0; i < pages.Count; i++)
        {
            bool isActivePage = i == pageIndex;

            pages[i].alpha = isActivePage ? 1.0f : 0.0f;
            pages[i].interactable = isActivePage;
            pages[i].blocksRaycasts = isActivePage;
        }

        if (Application.isPlaying)
            OnPageIndexChanged?.Invoke(pageIndex);

        var activePage = pages[pageIndex].gameObject;
        var firstSelectable = activePage.GetComponentInChildren<Selectable>();

        if (firstSelectable != null && EventSystem.current != null && Application.isPlaying)
            EventSystem.current.SetSelectedGameObject(firstSelectable.gameObject);
    }

    private void EnsureIndexIsInRange(int index)
    {
        if (tabs.Count == 0 || pages.Count == 0)
        {
            Debug.Log("Forgot to Setup Tabs or Pages");
            return;
        }

        pageIndex = Mathf.Clamp(index, 0, pages.Count - 1);
    }

    public void JumpToPage(int page)
    {
        EnsureIndexIsInRange(page);

        tabs[pageIndex].isOn = true;
    }
}
