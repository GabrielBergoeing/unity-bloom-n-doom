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

    // NetworkTime.time never advances without an active Mirror session, so time-gating
    // must fall back to plain Time.timeAsDouble offline or it would stay stuck forever.
    private double CurrentTime => (isServer || isClient) ? NetworkTime.time : Time.timeAsDouble;

    private void Awake()
    {
        col = GetComponent<Collider2D>();
    }

    private void OnEnable()
    {
        if (!gameObject.scene.isLoaded)
            return;

        bool isNetworkSpawnedObject = isServer || isClient;
        if (!isNetworkSpawnedObject || isServer)
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

    private void ResetPickupState()
    {
        pickupReadyTime = CurrentTime + pickupDelay;
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

    private void OnTriggerEnter2D(Collider2D other)
    {
        bool isNetworkSpawnedObject = isServer || isClient;
        if (isNetworkSpawnedObject && !isServer) return;

        if (isPickedUp) return;
        if (CurrentTime < pickupReadyTime) return;
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

    private void OnTriggerExit2D(Collider2D other)
    {
        bool isNetworkSpawnedObject = isServer || isClient;
        if (isNetworkSpawnedObject && !isServer) return;

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

    public void Pick(Player player)
    {
        bool isNetworkSpawnedObject = isServer || isClient;
        if (isNetworkSpawnedObject && !isServer) return;

        if (player == null) return;
        if (!canPickup || isPickedUp) return;

        isPickedUp = true;
        canPickup = false;
        player.pickupsInRange.Remove(this);
        playerInRange = null;
        OnPickup?.Invoke(player);
    }

    public void Drop(Player player, bool consume = false)
    {
        bool isNetworkSpawnedObject = isServer || isClient;
        if (isNetworkSpawnedObject && !isServer) return;

        isPickedUp = false;
        canPickup = false;
        pickupReadyTime = CurrentTime + pickupDelay;
        playerInRange = null;
        OnDrop?.Invoke(player);
    }

    public void Consume(Player player)
    {
        bool isNetworkSpawnedObject = isServer || isClient;
        if (isNetworkSpawnedObject && !isServer) return;

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
