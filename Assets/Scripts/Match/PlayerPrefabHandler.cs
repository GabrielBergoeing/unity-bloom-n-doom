using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerPrefabHandler : MonoBehaviour
{
    [SerializeField] private GameObject playerPrefab;
    private List<PlayerInput> activePlayers = new();

    private void Start()
    {
        SpawnPlayersFromConfig();
    }

    private void SpawnPlayersFromConfig()
    {
        var service = PlayerInputService.instance;
        var configs = service.Configs;

        for (int i = 0; i < configs.Count; i++)
        {
            var cfg = configs[i];
            var prefab = cfg.selectedCharacter.prefab;

            PlayerInput p = service.SpawnGameplayPlayer(
                i, prefab, cfg.controlScheme, cfg.device
            );

            p.transform.position = Vector3.zero;
            activePlayers.Add(p);
        }
    }
}
