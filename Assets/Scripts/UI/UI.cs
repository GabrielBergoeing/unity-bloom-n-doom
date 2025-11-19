using UnityEngine;

public class UI : MonoBehaviour
{
    public static UI instance;

    #region UI Components
    public UI_FadeScreen fadeScreen {get; private set;}
    public UI_SFX sfx {get; private set;}

    #endregion

    private void Awake()
    {
        instance = this;
        fadeScreen = GetComponentInChildren<UI_FadeScreen>();
        sfx = GetComponent<UI_SFX>();
    }
}
