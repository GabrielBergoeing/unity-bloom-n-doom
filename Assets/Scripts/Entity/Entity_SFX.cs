using UnityEngine;

public class Entity_SFX : MonoBehaviour
{
    protected AudioSource audioSource;

    [Header("SFX Names")]
    [SerializeField] private string move;

    private void Awake()
    {
        audioSource = GetComponentInChildren<AudioSource>();
    }

    public void PlayOnMovement()
    {
        if (!audioSource.isPlaying)
            AudioManager.instance.PlaySFX(move, audioSource);
    }
}