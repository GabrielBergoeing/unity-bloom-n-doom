using UnityEngine;

public class UIService : MonoBehaviour
{
    public static UIService instance;

    public UI_SFX sfx { get; private set; }
    public MenuManager menu { get; private set; }
    public UI_FadeScreen fade { get; private set; }

    public bool HasMenu => menu != null;
    private void Awake()
    {
        instance = this;
        sfx  = GetComponent<UI_SFX>();
        fade = GetComponentInChildren<UI_FadeScreen>();

        if (sfx == null)
            Debug.LogError("[UIService] Missing UI_SFX component!");

        if (fade == null)
            Debug.LogWarning("[UIService] No FadeScreen found (optional).");
    }
    private void Start()
    {
        menu = FindFirstObjectByType<MenuManager>();

        if (menu != null)
            Debug.Log("[UIService] MenuManager found in scene.");
    }    
}
