using UnityEngine;
using Mirror;

public class Pickup : NetworkBehaviour
{
    [Header("Item Settings")]
    public bool stackable;
    public int maxStackCount = 5; 
    public int itemId;

    [Header("Pickup Control")]
    [SyncVar] public bool canPickup = false;
    [SyncVar(hook = nameof(OnPickedUpChanged))] public bool isPickedUp = false;
    public float pickupDelay = 0.25f; 
    [SyncVar] private double pickupReadyTime;

    public System.Action<Player> OnPickup;
    public System.Action<Player> OnDrop;
    public Player playerInRange;

    private Collider2D col;

    private void Awake()
    {
        col = GetComponent<Collider2D>();
    }

    private void OnEnable()
    {
        if (isServer)
        {
            ResetPickupState();
        }

        UpdateColliderState();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        ResetPickupState();
    }

    [Server]
    private void ResetPickupState()
    {
        pickupReadyTime = NetworkTime.time + pickupDelay;
        canPickup = false;
        isPickedUp = false;
        playerInRange = null;
    }

    private void OnPickedUpChanged(bool _, bool __)
    {
        UpdateColliderState();
    }

    private void UpdateColliderState()
    {
        if (col != null)
            col.enabled = !isPickedUp;
    }

    [ServerCallback]
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isPickedUp) return;
        if (NetworkTime.time < pickupReadyTime) return;
        if (!other.CompareTag("Player")) return;

        Player player = other.GetComponent<Player>();
        if (player != null)
        {
            if (!player.pickupsInRange.Contains(this))
                player.pickupsInRange.Add(this);

            playerInRange = player;
            canPickup = true;
        }
    }

    [ServerCallback]
    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        Player player = other.GetComponent<Player>();
        if (player != null)
        {
            player.pickupsInRange.Remove(this);

            if (!isPickedUp && playerInRange == player)
            {
                playerInRange = null;
                canPickup = false;
            }
        }
    }

    [Server]
    public void Pick(Player player)
    {
        if (player == null) return;
        if (!canPickup || isPickedUp) return;

        isPickedUp = true;
        canPickup = false;
        player.pickupsInRange.Remove(this);
        playerInRange = null;
        OnPickup?.Invoke(player);
    }

    [Server]
    public void Drop(Player player, bool consume = false)
    {
        isPickedUp = false;
        canPickup = false;
        pickupReadyTime = NetworkTime.time + pickupDelay;
        playerInRange = null;
        OnDrop?.Invoke(player);
    }

    [Server]
    public void Consume(Player player)
    {
        if (player == null)
        {
            Debug.LogError("[PICKUP] Consume failed — player was NULL!");
            return;
        }

        Debug.Log($"[PICKUP] Consume by {player.name}");

        var hotbar = player.GetComponent<HotbarSystem>();
        if (hotbar != null)
            hotbar.RemoveItem(gameObject, consume: true);

        OnDrop?.Invoke(player);
    }
}
