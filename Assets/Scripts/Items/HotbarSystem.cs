using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Id + count based hotbar. Slots store item ids (indices into NetworkAssets.items)
/// instead of live GameObjects; the currently selected slot materializes a LOCAL
/// visual instance in the player's hand (never a networked object). The held item
/// id is replicated to other clients through NetworkPlayer so they can render it.
/// </summary>
public class HotbarSystem : MonoBehaviour
{
    public int numberOfSlots = 4;
    public int[] itemIds;      // -1 == empty
    public int[] stackCounts;
    public int currentSlot = 0;

    private GameObject handInstance; // local visual of the current slot's item
    private Transform playerHand;
    private PlayerInput playerInput;
    private Player player;

    private void Awake()
    {
        itemIds = new int[numberOfSlots];
        stackCounts = new int[numberOfSlots];
        for (int i = 0; i < numberOfSlots; i++)
            itemIds[i] = -1;

        playerInput = GetComponent<PlayerInput>();
        player = GetComponent<Player>();
        playerHand = transform.Find("OnHand");
    }

    private void Update()
    {
        if (playerInput == null || !playerInput.isActiveAndEnabled) return;

        if (playerInput.actions["Slot1"].triggered) SelectSlot(0);
        if (playerInput.actions["Slot2"].triggered) SelectSlot(1);
        if (playerInput.actions["Slot3"].triggered) SelectSlot(2);
        if (playerInput.actions["Slot4"].triggered) SelectSlot(3);
    }

    private void SelectSlot(int slotIndex)
    {
        if (slotIndex == currentSlot) return;
        currentSlot = slotIndex;
        RebuildHandVisual();
    }

    private void RebuildHandVisual()
    {
        if (handInstance != null)
        {
            Destroy(handInstance);
            handInstance = null;
        }

        int id = itemIds[currentSlot];
        if (id >= 0)
        {
            var prefab = NetworkAssets.Instance != null ? NetworkAssets.Instance.GetItemPrefab(id) : null;
            if (prefab != null && playerHand != null)
            {
                handInstance = Instantiate(prefab, playerHand);
                handInstance.transform.localPosition = Vector3.zero;
                handInstance.transform.localRotation = Quaternion.identity;
                ConfigureAsHeld(handInstance);
            }
        }

        // Replicate what we're holding so other clients can draw it in our hand.
        player?.net?.SetHeldItem(id);
    }

    private void ConfigureAsHeld(GameObject go)
    {
        foreach (var col in go.GetComponentsInChildren<Collider2D>(true))
            col.enabled = false;

        var rb = go.GetComponent<Rigidbody2D>();
        if (rb != null) rb.simulated = false;

        var pickup = go.GetComponent<Pickup>();
        if (pickup != null)
        {
            pickup.isPickedUp = true;
            pickup.canPickup = false;
            pickup.holder = player; // lets tool scripts (Flamethrower etc.) know who wields them
        }
    }

    // ======================================================
    //  INVENTORY OPERATIONS
    // ======================================================
    public bool AddItem(int itemId)
    {
        if (itemId < 0) return false;

        var prefab = NetworkAssets.Instance != null ? NetworkAssets.Instance.GetItemPrefab(itemId) : null;
        var pickupData = prefab != null ? prefab.GetComponent<Pickup>() : null;

        // Stack onto an existing slot first
        if (pickupData != null && pickupData.stackable)
        {
            for (int i = 0; i < itemIds.Length; i++)
            {
                if (itemIds[i] == itemId && stackCounts[i] < pickupData.maxStackCount)
                {
                    stackCounts[i]++;
                    return true;
                }
            }
        }

        // Otherwise take the first free slot
        for (int i = 0; i < itemIds.Length; i++)
        {
            if (itemIds[i] < 0)
            {
                itemIds[i] = itemId;
                stackCounts[i] = 1;
                if (i == currentSlot)
                    RebuildHandVisual();
                return true;
            }
        }

        return false; // hotbar full
    }

    /// <summary>Remove one unit of the current item without dropping it into the world.</summary>
    public void ConsumeCurrent()
    {
        int i = currentSlot;
        if (itemIds[i] < 0) return;

        stackCounts[i]--;
        if (stackCounts[i] <= 0)
        {
            itemIds[i] = -1;
            stackCounts[i] = 0;
        }
        RebuildHandVisual();
    }

    /// <summary>Remove one unit of the current item and spawn it back into the world.</summary>
    public void DropCurrent()
    {
        int i = currentSlot;
        int id = itemIds[i];
        if (id < 0) return;

        stackCounts[i]--;
        if (stackCounts[i] <= 0)
        {
            itemIds[i] = -1;
            stackCounts[i] = 0;
        }
        RebuildHandVisual();

        if (GameSession.OnlineActive)
        {
            GameSession.Instance?.RequestSpawnItemServerRpc(id, transform.position);
        }
        else
        {
            var prefab = NetworkAssets.Instance != null ? NetworkAssets.Instance.GetItemPrefab(id) : null;
            if (prefab != null)
                Instantiate(prefab, transform.position, Quaternion.identity);
        }
    }

    // ======================================================
    //  QUERIES
    // ======================================================
    public int GetCurrentSlot() => currentSlot;

    /// <summary>The live hand instance of the selected item (local visual), or null.</summary>
    public GameObject GetCurrentItem() => handInstance;

    public int GetCurrentItemId() => itemIds[currentSlot];

    public int GetSlotItemId(int slot) =>
        slot >= 0 && slot < itemIds.Length ? itemIds[slot] : -1;

    public int GetStackCount(int slot) =>
        slot >= 0 && slot < stackCounts.Length ? stackCounts[slot] : 0;
}
