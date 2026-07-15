using UnityEngine;

[RequireComponent(typeof(Pickup))]
public class Seed : MonoBehaviour
{
    [SerializeField] public GameObject plantPrefab;
    [SerializeField] private Pickup pickup;

    private void Start()
    {
        pickup = GetComponent<Pickup>();
    }
    
    // Called server-side from Player_PlantState via the state machine.
    public void Use(Vector3Int cell, Player player)
    {
        if (player == null || FarmManager.instance == null) return;
        FarmManager.instance.PlantSeed(cell, player.OwnerIndex, plantPrefab);
        pickup.Consume(player);
    }
}