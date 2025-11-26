using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class GameSceneLoader : MonoBehaviour
{
    private void Start()
    {
        PlayerInputService.instance.ResetForGameplay(); // reset runtime list
        var players = SpawnPlayersFromConfig();         // get gameplay players
        MatchManager.instance.InitializePlayers(players);
    }

    private List<PlayerInput> SpawnPlayersFromConfig()
    {
        var service = PlayerInputService.instance;
        var configs = service.Configs;
        var players = new List<PlayerInput>();

        for (int i = 0; i < configs.Count; i++)
        {
            var cfg = configs[i];

            PlayerInput p = service.SpawnGameplayPlayer(
                i,
                cfg.selectedCharacter.prefab,
                cfg.controlScheme,
                cfg.device
            );

            // Set temporary default spawn before MatchManager
            p.transform.position = Vector3.zero;

            // Assign Hotbar UI
            var hotbar = FindFirstObjectByType<UI_Hotbar>();
            var player = p.GetComponent<Player>();
            if (player != null && hotbar != null)
                hotbar.AssignHotbar(player.inventory);

            players.Add(p);
        }

        return players;
    }
}
