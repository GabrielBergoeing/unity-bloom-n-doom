using UnityEngine;
using UnityEngine.InputSystem;

//This script allows the player to pick up and drop items, is intended only for sabotage itmes such as the flamethrower
public class Pickup : MonoBehaviour
{
    public bool canPickup = false;

    public bool isPickedUp = false;
    private PlayerInput playerInput;
    private Player currentPlayer;
    private HotbarSystem hotbarSystem;
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
            if (isPickedUp)
            {
                DropItem();
            }
            else if (canPickup)
            {
                PickupItem();
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

    private void DropItem()
    {
        if (hotbarSystem != null)
        {
            isPickedUp = false;
            transform.SetParent(null);
            hotbarSystem.RemoveItem(gameObject);
            gameObject.SetActive(true);
            if (GetComponent<Collider2D>() != null)
            {
                GetComponent<Collider2D>().enabled = true;
            }
        }
    }


}
