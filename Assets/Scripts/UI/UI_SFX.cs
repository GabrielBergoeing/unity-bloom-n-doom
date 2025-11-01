using UnityEngine;

public class UI_SFX : MonoBehaviour
{
    protected AudioSource audioSource;

    [Header("SFX Names")]
    [SerializeField] private string confirm;

    private void Awake()
    {
        audioSource = GetComponentInChildren<AudioSource>();
    }

    public void PlayOnConfirm() => AudioManager.instance.PlaySFX(confirm, audioSource);
}