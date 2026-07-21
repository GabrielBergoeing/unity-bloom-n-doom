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

        // server-side owner assignment (remains useful for server logic)
        pickup.OnPickup += (player) => owner = player;
        pickup.OnDrop += (_) => owner = null;
    }

    private void Update()
    {
        // Attempt to resolve owner locally if not set (useful for clients:
        // HotbarSystem parentea el item bajo "OnHand" transform). Without this, owner is
        // only ever set by the OnPickup event above, which is server-only (Pickup.Pick() is
        // gated to isServer) - so on every client, including the one actually holding this
        // item, owner stayed null forever and Update() bailed out on the line below,
        // meaning the fertilizer never did anything online.
        if (owner == null)
        {
            owner = GetComponentInParent<Player>();
            if (owner == null) return;
        }

        // isLocalPlayer is never true offline (never Mirror-spawned), so gate on that
        // only when actually networked; offline just needs local input handling.
        bool isNetworkSpawnedObject = owner.isServer || owner.isClient;
        if (isNetworkSpawnedObject && !owner.isLocalPlayer) return;

        bool isUsing = owner.input.actions["Shoot"].ReadValue<float>() > 0f;

        if (isUsing)
        {
            if (TryUseFertilizer(isNetworkSpawnedObject))
            {
                // consume locally request server to destroy / handle stacks via HotbarSystem
                if (pickup != null)
                    pickup.Consume(owner);
            }
        }
    }

    private bool TryUseFertilizer(bool isNetworkSpawnedObject)
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

        if (isNetworkSpawnedObject)
            owner.CmdFertilizeCell(targetCell.x, targetCell.y, targetCell.z); // request server to fertilize
        else
            FarmManager.instance.TryFertilizePlant(targetCell);

        if (sfx != null)
        {
            sfx.PlayOnUse();
        }
        return true;
    }
}
