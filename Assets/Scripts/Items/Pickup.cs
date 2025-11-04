using UnityEngine;

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
        col.enabled = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (Time.time < pickupReadyTime) return;
        if (!other.CompareTag("Player")) return;

        Player player = other.GetComponent<Player>();
        if (player != null)
        {
            player.pickupsInRange.Add(this);
            playerInRange = player;
            canPickup = true;
        }
    }

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
    
    public void Pick(Player player)
    {
        if (!canPickup) return;

        isPickedUp = true;
        canPickup = false;
        col.enabled = false;
        OnPickup?.Invoke(player);
    }

    public void Drop(Player player, bool consume = false)
    {
        isPickedUp = false;
        col.enabled = true;
        OnDrop?.Invoke(player);
    }

    public void Consume(Player player)
    {
        var hotbar = player.GetComponent<HotbarSystem>();
        if (hotbar != null)
        {
            hotbar.RemoveItem(gameObject, consume: true);
        }

        OnDrop?.Invoke(player);
    }
}
