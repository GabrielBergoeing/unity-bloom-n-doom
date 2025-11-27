using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;

public class UI_CharacterSelector : MonoBehaviour
{
    private UIService UI => UIService.instance;

    [Header("Visual References")]
    [SerializeField] private Image portraitImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private CanvasGroup slotCanvas;
    [SerializeField] private GameObject lockIndicator;
    [SerializeField] private UI_CharacterArrowsAnimator arrows;

    [Header("Characters")]
    [SerializeField] private CharacterDatabase characterDB;

    [Header("Input")]
    [SerializeField] private float navCooldown = 0.25f;
    [SerializeField] private float navThreshold = 0.5f;

    [Header("Coloring")]
    [SerializeField] private Image panelBackground;
    [SerializeField] private Image borderImage;
    [SerializeField] private PanelColorTheme theme;

    // Internal
    private PlayerInput player;
    public PlayerInput AssignedPlayer => player;
    private int index = 0;
    private float cooldown;
    private System.Action<UI_CharacterSelector> notify;

    public bool IsOccupied => player != null;
    public bool IsLocked { get; private set; }
    public CharacterData SelectedCharacter => IsLocked ? characterDB.characters[index] : null;

    // ========================= ASSIGNMENT =========================

    public void AssignPlayer(PlayerInput input, System.Action<UI_CharacterSelector> callback)
    {
        player = input;
        player.SwitchCurrentActionMap("UI");

        notify = callback;
        IsLocked = false;

        RegisterControls();
        SetActiveVisuals();
        UpdateDisplay(true);
    }

    public void ClearAssignment()
    {
        UnregisterControls();
        player = null;
        IsLocked = false;
        notify = null;
        index = 0; // reset character cycling

        SetEmptyVisuals();
    }

    // ========================= INPUT =========================
    private void RegisterControls()
    {
        var nav = player.actions["Navigate"];
        var ok = player.actions["Submit"];
        var cancel = player.actions["Cancel"];

        if (nav != null) nav.performed += OnNavigate;
        if (ok != null) ok.performed += OnConfirm;
        if (cancel != null) cancel.performed += OnCancel;
    }

    private void UnregisterControls()
    {
        if (player == null) return;
        var nav = player.actions["Navigate"];
        var ok = player.actions["Submit"];
        var cancel = player.actions["Cancel"];

        if (nav != null) nav.performed -= OnNavigate;
        if (ok != null) ok.performed -= OnConfirm;
        if (cancel != null) cancel.performed -= OnCancel;
    }

    private void OnNavigate(InputAction.CallbackContext ctx)
    {
        if (!IsOccupied || IsLocked) return;
        if (Time.time < cooldown) return;

        float y = ctx.ReadValue<Vector2>().y; //up and down like castle crashers
        if (Mathf.Abs(y) < navThreshold) return;

        int count = characterDB.characters.Length;
        index = (index + (y > 0 ? 1 : -1) + count) % count;

        cooldown = Time.time + navCooldown;
        UI.sfx.PlayOnToggle();
        UpdateDisplay();
    }

    private void OnConfirm(InputAction.CallbackContext ctx)
    {
        if (!IsOccupied || IsLocked) return;
        UI.sfx.PlayOnConfirm();

        IsLocked = true;
        UpdateDisplay();
    }

    private void OnCancel(InputAction.CallbackContext ctx)
    {
        if (!IsOccupied) return;

        if (IsLocked)
        {
            IsLocked = false;
            UpdateDisplay();
            return;
        }
        UI.sfx.PlayOnHover();

        // Not locked → leave slot entirely
        ClearAssignment();
        notify?.Invoke(this);
    }

    // ========================= VISUALS =========================

    public void SetEmptyVisuals()
    {
        ApplyColor(PanelColorType.Empty);
        slotCanvas.alpha = 0.25f;
        lockIndicator.SetActive(false);
        nameText.text = "Press The Join Button!";
        portraitImage.sprite = null;
        arrows.Hide();
    }

    private void SetActiveVisuals()
    {
        slotCanvas.alpha = 1f;
        lockIndicator.SetActive(false);
        ApplyColor(PanelColorType.Active);
    }

    private void UpdateDisplay(bool forced = false)
    {
        if (!IsOccupied) return;

        var c = characterDB.characters[index];
        portraitImage.sprite = c.portrait;
        nameText.text = c.characterName;

        lockIndicator.SetActive(IsLocked);
        ApplyColor(IsLocked ? PanelColorType.Locked : PanelColorType.Active);

        // === Arrow Logic ===
        if (IsLocked)
            arrows.Hide();
        else
            arrows.Show();

        if (!forced) notify?.Invoke(this);
    }

    // ========================= COLORING =========================

    private void ApplyColor(PanelColorType type)
    {
        var t = GetCurrentTheme(); // <- NEW

        var s = type switch
        {
            PanelColorType.Empty => t.emptyColor,
            PanelColorType.Active => t.activeColor,
            PanelColorType.Locked => t.lockedColor,
            _ => t.emptyColor
        };

        panelBackground.color = s.background;
        borderImage.color = s.border;
        nameText.color = s.text;
    }

    private PanelColorTheme GetCurrentTheme()
    {
        // If character defines theme, use it
        if (IsOccupied && characterDB.characters[index].overrideTheme != null)
            return characterDB.characters[index].overrideTheme;

        // Otherwise use slot default
        return theme;
    }
}
