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
        if (player != null && player.isLocalPlayer)
        {
            // Enviar al servidor la petición. Usamos el nombre del prefab y lo cargamos en servidor.
            player.CmdRequestPlant(cell.x, cell.y, cell.z, plantPrefab.name);
            // consumo local de UI/inventario (ideal: autoritativo en servidor en futuro)
            pickup.Consume(player);
        }
        else if (player != null && player.isServer)
        {
            // Fallback para host
            FarmManager.instance.PlantSeed(cell, player.input.playerIndex, plantPrefab);
            pickup.Consume(player);
        }
    }
}