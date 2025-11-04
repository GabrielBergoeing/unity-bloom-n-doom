using UnityEngine;

[RequireComponent(typeof(Pickup))]
public class Seed : MonoBehaviour
{
    [SerializeField] private GameObject plantPrefab;
    [SerializeField] private Pickup pickup;
    private Player owner;

    private void Start()
    {
        pickup = GetComponent<Pickup>();
        pickup.OnPickup += (player) => owner = player;
        pickup.OnDrop += (_) => owner = null;
    }
    
    public void Use(Vector3Int cell, int playerIndex)
    {
        FarmManager.instance.PlantSeed(cell, playerIndex, plantPrefab);

        // Consume the seed from inventory
        var pickup = GetComponent<Pickup>();
        if (pickup != null)
        {
            pickup.Consume(owner);
        }
    }
}