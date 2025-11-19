using UnityEngine;
using UnityEngine.InputSystem;

public class UI_SettingsMenu : UI
{
    [Header("Input Actions")]
    public InputActionAsset inputActions;

    [Header("References")]
    public UI_InputFactory inputFactory;
    public UI_TabMenu tabMenu;

    [Header("Controls Tab")]
    public int controlsPageIndex = 2;
    public bool IsControlsTabOpen => 
        tabMenu != null && tabMenu.CurrentPageIndex == controlsPageIndex;
    
    private bool isRegenerating = false;

    private void Start()
    {
        tabMenu.OnPageIndexChanged.AddListener(OnPageChanged);
    }

    private void OnDestroy()
    {
        tabMenu.OnPageIndexChanged.RemoveListener(OnPageChanged);
    }

    private void OnPageChanged(int pageIndex)
    {
        if (pageIndex == controlsPageIndex)
            GenerateControlBindings();
    }

    private void GenerateControlBindings()
    {
        if (isRegenerating) return;
        isRegenerating = true;

        inputFactory.Clear();

        try
        {
            var service = PlayerInputService.instance;
            var configs = service.Configs;

            // ----------------------------------------------------
            // CASE 1 — We have real players (from lobby)
            // ----------------------------------------------------
            if (configs.Count > 0)
            {
                Debug.Log("[Settings] Using PlayerInputService configs.");

                for (int i = 0; i < configs.Count; i++)
                {
                    var cfg = configs[i];
                    PlayerInput p = service.GetPlayerByDevice(cfg.device);

                    if (p == null)
                    {
                        Debug.LogWarning(
                            $"[Settings] No PlayerInput for config {i}. Using assigned InputActions."
                        );

                        InputActionMap cfgMap = inputActions.FindActionMap("Player", false);
                        if (cfgMap != null)
                            inputFactory.Generate(cfgMap, i);

                        continue;
                    }

                    InputActionMap realMap = p.actions.FindActionMap("Player", false);
                    if (realMap != null)
                        inputFactory.Generate(realMap, i);
                }

                return;
            }

            // ----------------------------------------------------
            // CASE 2 — No players exist (System Menu)
            // ----------------------------------------------------
            Debug.Log("[Settings] No player configs → generating from assigned InputActions.");

            if (inputActions == null)
            {
                Debug.LogError("[Settings] No inputActions asset assigned in inspector!");
                return;
            }

            InputActionMap defaultMap = inputActions.FindActionMap("Player", false);

            if (defaultMap == null)
            {
                Debug.LogError("[Settings] InputActions asset has no 'Player' map!");
                return;
            }

            inputFactory.Generate(defaultMap, 0);
        }
        finally
        {
            isRegenerating = false;
        }
    }


    private PlayerInput GetPlayerInputFromDevice(InputDevice dev)
    {
        foreach (var p in PlayerInput.all)
        {
            if (p != null && p.OwnsDevice(dev))
            {
                Debug.Log($"[SettingsMenu] Matched device {dev.displayName} → PlayerInput {p.playerIndex}");
                return p;
            }
        }

        Debug.LogWarning($"[SettingsMenu] No PlayerInput owns device: {dev.displayName}");
        return null;
    }

    public void ReturnBTN()
    {
        sfx.PlayOnConfirm();
        GameManager.instance.ChangeScene("MainMenu");
    }

    public void RegenerateControlsImmediate()
    {
        GenerateControlBindings();
    }
}
