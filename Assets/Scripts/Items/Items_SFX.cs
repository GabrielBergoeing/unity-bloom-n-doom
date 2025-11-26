using UnityEngine;

public class Items_SFX : Entity_SFX
{
    [SerializeField] private string useSound;

    public void PlayOnUse() => AudioManager.instance.PlaySFX(useSound, audioSource);
}
