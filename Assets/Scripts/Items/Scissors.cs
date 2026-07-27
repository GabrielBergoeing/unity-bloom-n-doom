using UnityEngine;

[RequireComponent(typeof(Pickup))]
public class Scissors : MonoBehaviour
{
    [Header("Scissors Durations (on frames)")]
    [SerializeField] private float cutDuration = 0f;
    [SerializeField] private float cooldown = 0f;
    private bool isOnCooldown = false;

    public float CutDuration => cutDuration;
    public float Cooldown => cooldown;
    public bool IsOnCooldown => isOnCooldown;

    public Items_SFX sfx { get; private set; }
    private Pickup pickup;

    private void Awake()
    {
        sfx = GetComponent<Items_SFX>();
        pickup = GetComponent<Pickup>();
    }

    public void Use(Vector3Int targetCell, Player player)
    {
        if (isOnCooldown)
        {
            Debug.Log("Scissors cooldown");
            return;
        }
        sfx.PlayOnUse();

        if (FarmManager.instance.IsOccupied(targetCell))
        {
            FarmManager.instance.RemovePlant(targetCell);
            Debug.Log($"Planta en {targetCell} cortada con éxito.");
        }
        else
        {
            Debug.Log("No hay planta para cortar.");
        }

        isOnCooldown = true;

        // Single-use tool - consumed from the inventory on every swing (hit or miss),
        // same as the cooldown above always applying regardless of outcome.
        pickup.Consume(player);
    }

    public void ResetCooldown() => isOnCooldown = false;
}
