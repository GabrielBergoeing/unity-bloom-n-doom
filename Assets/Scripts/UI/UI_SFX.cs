using UnityEngine;

public class UI_SFX : MonoBehaviour
{
    protected AudioSource audioSource;

    [Header("SFX Names")]
    [SerializeField] private string confirm;
    [SerializeField] private string toggle;
    [SerializeField] private string hover;

    private void Awake()
    {
        audioSource = GetComponentInChildren<AudioSource>();
    }

    public void PlayOnConfirm() => AudioManager.instance.PlaySFX(confirm, audioSource);
    public void PlayOnToggle() => AudioManager.instance.PlaySFX(toggle, audioSource);
    public void PlayOnHover() => AudioManager.instance.PlaySFX(hover, audioSource);
}