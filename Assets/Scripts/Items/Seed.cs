using UnityEngine;

[RequireComponent(typeof(Pickup))]
public class Seed : MonoBehaviour
{
    [SerializeField] private GameObject plantPrefab;
    [SerializeField] private Pickup pickup;

    /// <summary>The plant this seed grows into (used by the server to spawn it).</summary>
    public GameObject PlantPrefab => plantPrefab;

    private void Awake()
    {
        pickup = GetComponent<Pickup>();
    }

    public void Use(Vector3Int cell, Player player)
    {
        if (GameSession.OnlineActive)
        {
            // Ask the server to plant; consume the seed locally right away.
            player.net?.RequestPlantSeed(cell, pickup.itemId);
            pickup.Consume(player);
            return;
        }

        FarmManager.instance.PlantSeed(cell, player.PlayerIndex, plantPrefab);
        pickup.Consume(player);
    }
}
