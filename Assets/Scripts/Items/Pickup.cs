using System.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Analytics;
using UnityEngine.InputSystem;

//This script allows the player to pick up and drop items, is intended only for sabotage itmes such as the flamethrower
public class Pickup : MonoBehaviour
{
    public bool canPickup = false;

    public bool isPickedUp = false;
    public bool stackable = false; //Indicates if the item can be stacked in the hotbar
    public int maxStackCount = 5; // The maximum stack count of the item

    public int itemId; // Unique identifier for the item type
    public PlayerInput playerInput;
    public Player currentPlayer;
    public HotbarSystem hotbarSystem;
    private Player savedPlayer;
    private HotbarSystem savedHotbarSystem;
    private PlayerInput savedPlayerInput;


    void Start()
    {

    }
    void Update()
    {
        if (playerInput != null && playerInput.actions["Pickup"].triggered)
        {
            if (canPickup)
            {
                PickupItem();
            }
        }
        if (playerInput != null && playerInput.actions["Drop"].triggered)
        {
            if (isPickedUp)
            {
                DropItem();
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            currentPlayer = other.GetComponent<Player>();
            playerInput = currentPlayer.GetComponent<PlayerInput>();
            hotbarSystem = currentPlayer.GetComponent<HotbarSystem>();
            canPickup = true;
            savedPlayer = currentPlayer;
            savedHotbarSystem = hotbarSystem;
            savedPlayerInput = playerInput;
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player") && !isPickedUp)
        {
            currentPlayer = null;
            playerInput = null;
            hotbarSystem = null;
            canPickup = false;
        }
    }
    private void PickupItem()
    {
        if (hotbarSystem != null)
        {
            if (hotbarSystem.AddItem(gameObject))
            {
                canPickup = false;
                currentPlayer = savedPlayer;
                hotbarSystem = savedHotbarSystem;
                playerInput = savedPlayerInput;
                isPickedUp = true;
                if (GetComponent<Collider2D>() != null)
                {
                    GetComponent<Collider2D>().enabled = false;
                }
            }
        }
    }

    public void DropItem(bool consume = false)
    {
        if (hotbarSystem != null)
        {
            if (consume)
            {
                // If consuming, just decrease the stack count without dropping
                if (hotbarSystem.stackCounts[hotbarSystem.currentSlot] == 1)
                {
                    isPickedUp = false;
                    hotbarSystem.RemoveItem(gameObject);
                    Destroy(gameObject); // Optionally destroy the item
                }
                else
                {
                    hotbarSystem.RemoveItem(gameObject);
                }
                return;
            }
            
            if (hotbarSystem.stackCounts[hotbarSystem.currentSlot] == 1)
            {
                isPickedUp = false;
                transform.SetParent(null);
                hotbarSystem.RemoveItem(gameObject);
                gameObject.SetActive(true);
            }
            else
            {
                hotbarSystem.RemoveItem(gameObject);
            }
            if (GetComponent<Collider2D>() != null)
            {
                GetComponent<Collider2D>().enabled = true;
            }
        }
    }


}
