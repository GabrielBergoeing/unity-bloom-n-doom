using System.Globalization;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

public class HotbarSystem : NetworkBehaviour
{
    public GameObject[] slots;
    public int[] stackCounts;
    public int currentSlot = 0;
    public int numberOfSlots = 4;
    private Transform playerHand;
    private Transform playerInventory;
    private PlayerInput playerInput;

    void Start()
    {
        EnsureRuntimeSetup();

    }

    void Update()
    {
        if (playerInput == null) return;

        // isLocalPlayer is never true offline (never Mirror-spawned), so gate on that
        // only when actually networked; offline just needs local input handling.
        bool isNetworkSpawnedObject = isServer || isClient;
        if (isNetworkSpawnedObject && !isLocalPlayer) return;

        if (playerInput.actions["Slot1"].triggered) RequestSelectSlot(0);
        if (playerInput.actions["Slot2"].triggered) RequestSelectSlot(1);
        if (playerInput.actions["Slot3"].triggered) RequestSelectSlot(2);
        if (playerInput.actions["Slot4"].triggered) RequestSelectSlot(3);
    }

    private void RequestSelectSlot(int slotIndex)
    {
        bool isNetworkSpawnedObject = isServer || isClient;
        if (isNetworkSpawnedObject)
            CmdSelectSlot(slotIndex);
        else
            SelectSlot(slotIndex);
    }

    private void EnsureRuntimeSetup()
    {
        if (slots == null || slots.Length != numberOfSlots)
            slots = new GameObject[numberOfSlots];

        if (stackCounts == null || stackCounts.Length != numberOfSlots)
            stackCounts = new int[numberOfSlots];

        if (playerInput == null)
            playerInput = GetComponent<PlayerInput>();

        if (playerHand == null)
            playerHand = transform.Find("OnHand");
    }

    [Command]
    private void CmdSelectSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= numberOfSlots) return;

        SelectSlot(slotIndex);
        RpcSelectSlot(slotIndex);
    }

    [ClientRpc]
    private void RpcSelectSlot(int slotIndex)
    {
        if (isServer) return;
        if (slotIndex < 0 || slotIndex >= numberOfSlots) return;
        SelectSlot(slotIndex);
    }

    private void SelectSlot(int slotIndex)
    {
        EnsureRuntimeSetup();

        if (slots[currentSlot] != null)
        {
            slots[currentSlot].SetActive(false);
        }

        currentSlot = slotIndex;

        if (slots[currentSlot] != null)
        {
            slots[currentSlot].SetActive(true);
            slots[currentSlot].transform.SetParent(playerHand);
            slots[currentSlot].transform.localPosition = Vector3.zero;
            slots[currentSlot].transform.localRotation = Quaternion.identity;
        }
    }

    public bool AddItem(GameObject item)
    {
        EnsureRuntimeSetup();

        Pickup pickup = item.GetComponent<Pickup>();
        if (pickup != null && pickup.stackable)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null && slots[i].GetComponent<Pickup>().itemId == pickup.itemId && stackCounts[i] < pickup.maxStackCount)
                {
                    stackCounts[i]++;

                    if (isServer)
                    {
                        RpcUpdateStackCount(i, stackCounts[i]);
                        NetworkServer.Destroy(item);
                    }
                    else if (!isClient)
                    {
                        Destroy(item);
                    }

                    return true;
                }
            }
        }

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null)
            {
                slots[i] = item;
                stackCounts[i] = 1;

                if (i != currentSlot)
                {
                    item.SetActive(false);
                }
                else
                {
                    item.transform.SetParent(playerHand);
                    item.transform.localPosition = Vector3.zero;
                    item.transform.localRotation = Quaternion.identity;
                }

                if (isServer)
                {
                    var identity = item.GetComponent<NetworkIdentity>();
                    if (identity != null)
                    {
                        RpcRegisterSlotItem(i, identity.netId, stackCounts[i], i == currentSlot);
                    }
                }

                return true;
            }
        }
        return false;
    }

    [ClientRpc]
    private void RpcUpdateStackCount(int slotIndex, int stackCount)
    {
        if (slotIndex < 0 || slotIndex >= stackCounts.Length) return;
        stackCounts[slotIndex] = stackCount;
    }

    [ClientRpc]
    private void RpcRegisterSlotItem(int slotIndex, uint itemNetId, int stackCount, bool activeInHand)
    {
        if (isServer) return;

        EnsureRuntimeSetup();
        if (slotIndex < 0 || slotIndex >= slots.Length) return;

        if (!NetworkClient.spawned.TryGetValue(itemNetId, out var identity) || identity == null)
            return;

        var item = identity.gameObject;
        slots[slotIndex] = item;
        stackCounts[slotIndex] = stackCount;

        if (activeInHand)
        {
            item.SetActive(true);
            item.transform.SetParent(playerHand);
            item.transform.localPosition = Vector3.zero;
            item.transform.localRotation = Quaternion.identity;
        }
        else
        {
            item.SetActive(false);
        }
    }

    public void RemoveItem(GameObject item, bool consume = false)
    {
        EnsureRuntimeSetup();

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == item)
            {
                stackCounts[i]--;

                // If items still in stack
                if (stackCounts[i] > 0)
                {
                    if (!consume)
                    {
                        GameObject droppedCopy = Instantiate(item, transform.position, Quaternion.identity);
                        droppedCopy.GetComponent<Pickup>().stackable = true;

                        if (isServer)
                        {
                            var droppedIdentity = droppedCopy.GetComponent<NetworkIdentity>();
                            if (droppedIdentity != null)
                                NetworkServer.Spawn(droppedCopy);

                            RpcUpdateStackCount(i, stackCounts[i]);
                        }
                    }
                }
                else
                {
                    slots[i] = null;
                    stackCounts[i] = 0;

                    // Last item consumed → destroy it
                    if (consume)
                    {
                        if (isServer)
                            NetworkServer.Destroy(item);
                        else
                            Destroy(item);
                    }
                    else
                    {
                        item.transform.SetParent(null);
                        item.SetActive(true);
                    }

                    if (isServer)
                    {
                        RpcClearSlot(i, consume);
                    }
                }

                return; // ✅ stop here
            }
        }
    }

    [ClientRpc]
    private void RpcClearSlot(int slotIndex, bool consumed)
    {
        if (isServer) return;
        if (slotIndex < 0 || slotIndex >= slots.Length) return;

        var item = slots[slotIndex];
        slots[slotIndex] = null;
        stackCounts[slotIndex] = 0;

        if (item != null && !consumed)
        {
            item.transform.SetParent(null);
            item.SetActive(true);
        }
    }

    public int GetCurrentSlot()
    {
        return currentSlot;
    }

    public GameObject GetCurrentItem()
    {
        return slots[currentSlot];
    }
}