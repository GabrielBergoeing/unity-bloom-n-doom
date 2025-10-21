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
    
    [Header("Plant Durations (on frames)")]
    [SerializeField] private float plantDuration = 0f;
    [SerializeField] private float cooldown = 0f;
    private bool isOnCooldown = false;

    public float PlantDuration => plantDuration;
    public float Cooldown => cooldown;
    public bool IsOnCooldown => isOnCooldown;

    //[SerializeField] private int growthTime = 5;

    private void Start()
    {
        pickup = GetComponent<Pickup>();
    }

    private void OnPickup()
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

    private void Update()
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

    public void Use(Vector3Int targetCell)
    {
        if (isOnCooldown)
        {
            Debug.Log("Planting cooldown");
            return;
        }

        if (FarmManager.instance.IsPrepared(targetCell))
        {
            farmManager.PlantSeed(targetCell, playerInput.playerIndex);
        }
        else
        {
            Debug.Log("No se puede plantar en este espacio");
        }

        isOnCooldown = true;
    }

    public void ResetCooldown() => isOnCooldown = false;
}
