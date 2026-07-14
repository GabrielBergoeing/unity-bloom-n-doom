using UnityEngine;

public class Fertilizer : MonoBehaviour
{
    [SerializeField] private Pickup pickup;

    // Who currently wields it: set through Pickup.holder on the hand instance.
    private Player owner => pickup != null ? pickup.holder : null;

    public Items_SFX sfx { get; private set; }

    private void Awake()
    {
        sfx = GetComponent<Items_SFX>();
        pickup = GetComponent<Pickup>();
    }

    private void Update()
    {
        if (owner == null) return;
        if (!owner.IsLocalOwner) return;

        bool isUsing = owner.input.actions["Shoot"].ReadValue<float>() > 0f;

        if (isUsing)
        {
            if (TryUseFertilizer())
            {
                pickup.Consume(owner);
            }
        }
    }

    private bool TryUseFertilizer()
    {
        if (owner == null) return false;

        TileInteraction tileInteraction = owner.tile;
        if (tileInteraction == null) 
        {
            return false;
        }
        Vector3Int targetCell = tileInteraction.CurrentCell;
        if (!FarmManager.instance.HasPlant(targetCell)) 
        {
            return false;
        }
        tileInteraction.FertilizeInCell();
        
        if (sfx != null)
        {
            sfx.PlayOnUse();
        }
        return true;
    }
}
