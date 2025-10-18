using UnityEngine;
using UnityEngine.InputSystem;

//This script allows the player to pick up and drop items, is intended only for sabotage itmes such as the flamethrower
public class Pickup : MonoBehaviour
{
    private bool canPickup = false;

    private bool isPickedUp = false;
    private Transform playerHand;
    private PlayerInput playerInput;
    private Player currentPlayer;

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
            playerHand = currentPlayer.transform.Find("OnHand");
            canPickup = true;
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player") && !isPickedUp)
        {
            currentPlayer = null;
            playerInput = null;
            canPickup = false;
            playerHand = null;
        }
    }

    private void PickupItem()
    {
        if (playerHand != null)
        {
            isPickedUp = true;
            transform.SetParent(playerHand);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;

            if (GetComponent<Collider2D>() != null)
            {
                GetComponent<Collider2D>().enabled = false;
            }
        }
    }
    private void DropItem()
    {
        isPickedUp = false;
        transform.SetParent(null);
        if (GetComponent<Collider2D>() != null)
        {
            GetComponent<Collider2D>().enabled = true;
        }
    }


}
