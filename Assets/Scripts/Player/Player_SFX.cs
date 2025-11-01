using UnityEngine;

public class Player_SFX : Entity_SFX
{
    [SerializeField] private string irrigate;
    [SerializeField] private string pick;
    [SerializeField] private string plant;
    [SerializeField] private string prepareGround;
    [SerializeField] private string remove;
    [SerializeField] private string sabotage;

    public void PlayOnIrrigate() => AudioManager.instance.PlaySFX(irrigate, audioSource);
    public void PlayOnPick() => AudioManager.instance.PlaySFX(pick, audioSource);
    public void PlayOnPlant() => AudioManager.instance.PlaySFX(plant, audioSource);
    public void PlayOnPrepareGround() => AudioManager.instance.PlaySFX(prepareGround, audioSource);
    public void PlayOnRemove() => AudioManager.instance.PlaySFX(remove, audioSource);
    public void PlayOnSabotage() => AudioManager.instance.PlaySFX(sabotage, audioSource);
}
