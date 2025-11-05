using UnityEngine;

[RequireComponent(typeof(Pickup))]
public class Seed : MonoBehaviour
{
    [SerializeField] private GameObject plantPrefab;
    [SerializeField] private Pickup pickup;

    private void Start()
    {
        pickup = GetComponent<Pickup>();
    }
    
    public void Use(Vector3Int cell, Player player)
    {
        FarmManager.instance.PlantSeed(cell, player.input.playerIndex, plantPrefab);
        pickup.Consume(player);
    }

}