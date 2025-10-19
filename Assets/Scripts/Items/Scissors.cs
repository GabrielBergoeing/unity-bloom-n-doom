using UnityEngine;

[RequireComponent(typeof(Pickup))]
public class Scissors : MonoBehaviour
{
    [Header("Scissors Settings")]
    [SerializeField] private float cutDuration = 0f;
    [SerializeField] private float cooldown = 0f;
    private bool isOnCooldown = false;

    public float CutDuration => cutDuration;
    public float Cooldown => cooldown;
    public bool IsOnCooldown => isOnCooldown;

    public void Use(Vector3Int targetCell)
    {
        if (isOnCooldown)
        {
            Debug.Log("Scissors cooldown");
            return;
        }

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
    }

    public void ResetCooldown() => isOnCooldown = false;
}
