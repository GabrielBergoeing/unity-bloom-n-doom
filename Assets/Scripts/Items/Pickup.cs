using Mirror;
using UnityEngine;

public class Pickup : NetworkBehaviour
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

    private Collider2D col;

    private void Awake()
    {
        col = GetComponent<Collider2D>();
    }

    private void OnEnable()
    {
        pickupReadyTime = Time.time + pickupDelay;
        canPickup = false;
        isPickedUp = false;
        if (col != null) col.enabled = true;
    }

    private void Update()
    {
        if (!canPickup && Time.time >= pickupReadyTime)
            canPickup = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        // Use GetComponentInParent para soportar colliders en hijos del jugador
        var player = other.GetComponentInParent<Player>();
        if (player == null) return;

        playerInRange = player;

        if (!player.pickupsInRange.Contains(this))
            player.pickupsInRange.Add(this);

        // Opcional: ayuda de debug si no aparece en consola
        // Debug.Log($"[PICKUP] Player entered range: {player.netId}", this);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        var player = other.GetComponentInParent<Player>();
        if (player == null) return;

        if (player.pickupsInRange.Contains(this))
            player.pickupsInRange.Remove(this);

        if (playerInRange == player)
            playerInRange = null;

        // Debug.Log($"[PICKUP] Player exited range: {player.netId}", this);
    }

    public void Pick(Player player)
    {
        if (!isServer) return;
        if (!canPickup) return;

        isPickedUp = true;
        canPickup = false;
        if (col != null) col.enabled = false;

        RpcOnPicked();

        OnPickup?.Invoke(player);
    }

    public void Drop(Player player, bool consume = false)
    {
        if (!isServer) return;

        isPickedUp = false;
        if (col != null) col.enabled = true;

        OnDrop?.Invoke(player);
    }

    public void Consume(Player player)
    {
        if (!isServer) return;

        if (player == null)
        {
            Debug.LogError("[PICKUP] Consume failed — player was NULL!");
            return;
        }

        var hotbar = player.GetComponent<HotbarSystem>();
        if (hotbar != null)
            hotbar.RemoveItem(gameObject, consume: true);

        OnDrop?.Invoke(player);
    }

    [ClientRpc]
    void RpcOnPicked()
    {
        if (col != null) col.enabled = false;
    }
}
