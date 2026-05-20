using UnityEngine;

public class Fertilizer : MonoBehaviour
{
    [SerializeField] private Pickup pickup;
    private Player owner;

    public Items_SFX sfx { get; private set; }

    private void Awake()
    {
        sfx = GetComponent<Items_SFX>();
    }

    private void Start()
    {
        pickup = GetComponent<Pickup>();
        pickup.OnPickup += (player) => owner = player;
        pickup.OnDrop += (_) => owner = null;
    }

    private void Update()
    {
        if (owner == null) return;

        // only local owner sends the request
        if (!owner.isLocalPlayer) return;

        bool isUsing = owner.input.actions["Shoot"].ReadValue<float>() > 0f;

        if (isUsing)
        {
            if (TryUseFertilizer())
            {
                // consume locally request server to destroy / handle stacks via HotbarSystem
                if (pickup != null)
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

        // Request server to fertilize via Player command
        owner.CmdFertilizeCell(targetCell.x, targetCell.y, targetCell.z);
        
        if (sfx != null)
        {
            sfx.PlayOnUse();
        }
        return true;
    }
}
