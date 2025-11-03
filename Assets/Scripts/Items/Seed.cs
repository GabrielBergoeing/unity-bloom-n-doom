using UnityEngine;

[RequireComponent(typeof(Pickup))]
public class Seed : MonoBehaviour
{
    [SerializeField] private GameObject plantPrefab;

    public void Use(Vector3Int cell, int playerIndex)
    {
        FarmManager.instance.PlantSeed(cell, playerIndex, plantPrefab);

        // Consume the seed from inventory
        var pickup = GetComponent<Pickup>();
        if (pickup != null)
        {
            pickup.DropItem(consume: true);
        }
    }
}

