using UnityEngine;

/// <summary>
/// World pickup. Online, world items are server-spawned NetworkObjects; picking one
/// up is requested by the owner (Player_PickState -> NetworkPlayer.RequestPickup),
/// validated by the server, then granted into the local id-based hotbar.
/// Trigger tracking only reacts to the locally-controlled player.
/// </summary>
public class Pickup : MonoBehaviour
{
    [Header("Item Settings")]
    public bool stackable;
    public int maxStackCount = 5;
    public int itemId;

    [Header("Pickup Control")]
    public bool canPickup = false;
    public bool isPickedUp = false;
    public float pickupDelay = 0.25f;
    private float pickupReadyTime;

    public System.Action<Player> OnPickup;
    public System.Action<Player> OnDrop;
    public Player playerInRange;

    /// <summary>Set on held hand-instances so tool scripts know who wields them.</summary>
    [HideInInspector] public Player holder;

    private Collider2D col;

    private void Awake()
    {
        col = GetComponent<Collider2D>();
    }

    private void OnEnable()
    {
        pickupReadyTime = Time.time + pickupDelay;
        if (isPickedUp) return; // hand instances stay inert

        canPickup = false;
        if (col != null) col.enabled = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (Time.time < pickupReadyTime) return;
        if (!other.CompareTag("Player")) return;

        Player player = other.GetComponent<Player>();
        if (player == null || !player.IsLocalOwner) return; // only track the local player

        player.pickupsInRange.Add(this);
        playerInRange = player;
        canPickup = true;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        Player player = other.GetComponent<Player>();
        if (player == null || !player.IsLocalOwner) return;

        player.pickupsInRange.Remove(this);

        if (!isPickedUp && playerInRange == player)
        {
            playerInRange = null;
            canPickup = false;
        }
    }

    public void Pick(Player player)
    {
        if (!canPickup) return;

        isPickedUp = true;
        canPickup = false;
        holder = player;
        if (col != null) col.enabled = false;
        OnPickup?.Invoke(player);
    }

    public void Drop(Player player, bool consume = false)
    {
        isPickedUp = false;
        holder = null;
        if (col != null) col.enabled = true;
        OnDrop?.Invoke(player);
    }

    /// <summary>Remove one unit of this item from the holder's hotbar (used up).</summary>
    public void Consume(Player player)
    {
        if (player == null)
        {
            Debug.LogError("[PICKUP] Consume failed — player was NULL!");
            return;
        }

        player.inventory?.ConsumeCurrent();
        OnDrop?.Invoke(player);
    }
}
