using System.Globalization;
using UnityEngine;
using UnityEngine.InputSystem;

public class HotbarSystem : MonoBehaviour
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
    slots = new GameObject[numberOfSlots];
    stackCounts = new int[numberOfSlots];
    playerInput = GetComponent<PlayerInput>();
    playerHand = transform.Find("OnHand");

    }

    void Update()
    {
        if (playerInput.actions["Slot1"].triggered) SelectSlot(0);
        if (playerInput.actions["Slot2"].triggered) SelectSlot(1);
        if (playerInput.actions["Slot3"].triggered) SelectSlot(2);
        if (playerInput.actions["Slot4"].triggered) SelectSlot(3);
    }

    private void SelectSlot(int slotIndex)
    {
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
        Pickup pickup = item.GetComponent<Pickup>();
        if (pickup != null && pickup.stackable)
        {
            // First try to find an existing stack of the same item type
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null && slots[i].GetComponent<Pickup>().itemId == pickup.itemId && stackCounts[i] < pickup.maxStackCount)
                {
                    stackCounts[i]++;
                    // If we're stacking, we can destroy the new item instance
                    Destroy(item);
                    return true;
                }
            }
        }

        // If we couldn't stack, find an empty slot
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
                return true;
            }
        }
        return false;
    }

    public void RemoveItem(GameObject item)
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == item)
            {
                if (stackCounts[i] > 1){
                    GameObject droppedItem = Instantiate(item, transform.position, transform.rotation);
                    droppedItem.GetComponent<Pickup>().stackable = true;
                }
                stackCounts[i]--;
                if (stackCounts[i] <= 0)
                {
                    slots[i] = null;
                    stackCounts[i] = 0;
                }
                break;
            }
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