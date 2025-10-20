using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Pickup))]
public class Seed : MonoBehaviour
{
    [SerializeField] private Pickup pickup;
    [SerializeField] private FarmManager farmManager;
    [SerializeField] private TileInteraction tileInteraction;
    [SerializeField] private PlayerInput playerInput;
    private bool hasExecutedPickup = false;

    //[SerializeField] private int growthTime = 5;

    void Start()
    {
        pickup = GetComponent<Pickup>();
    }

    void OnPickup()
    {
        farmManager = FarmManager.instance;
        playerInput = GetComponentInParent<PlayerInput>();

        if (playerInput != null)
        {
            Transform tileController = playerInput.transform.Find("TileController");
            if (tileController != null)
            {
                tileInteraction = tileController.GetComponent<TileInteraction>();
            }
        }
    }

    void Update()
    {


        if (pickup.isPickedUp == true && !hasExecutedPickup)
        {
            OnPickup();
            hasExecutedPickup = true;
            Debug.Log("Semilla recogida por el jugador");
        }
        else if (!pickup.isPickedUp)
        {
            tileInteraction = null;
            playerInput = null;
            hasExecutedPickup = false;
        }
        if (pickup.isPickedUp)
        {
            if (playerInput != null && playerInput.actions["Interact"].triggered)
            {
                Vector3Int targetCell = tileInteraction.CurrentCell;
                if (farmManager.IsPrepared(targetCell) && !farmManager.IsOccupied(targetCell))
                {
                    farmManager.PlantSeed(targetCell, playerInput.playerIndex);
                    pickup.DropItem(true); // Drop the seed after planting
                }
                else
                {
                    Debug.Log("No se puede plantar aquí.");
                }
            }
        }
    }

}
