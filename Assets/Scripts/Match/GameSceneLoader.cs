using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class GameSceneLoader : MonoBehaviour
{
    private void Start()
    {
        // The PlayerInputManager is kept enabled through the lobby -> gameplay transition
        // (see UI_MatchMenu.StartMatch) so local players can still join. Left enabled here,
        // any stray button/key press races our manual PlayerInput.Instantiate() calls below
        // and can auto-join a phantom keyboard player that steals index 0 - which is why
        // player 1 would end up locked to keyboard even when using a controller.
        if (PlayerInputManager.instance != null)
            PlayerInputManager.instance.DisableJoining();

        var service = PlayerInputService.instance;

        if (service == null)
        {
            Debug.LogError("[GameSceneLoader] PlayerInputService not found!");
            return;
        }

        if (service.Configs == null || service.Configs.Count == 0)
        {
            Debug.LogError("[GameSceneLoader] No player configs found! Players not set up correctly in lobby.");
            return;
        }

        service.ResetForGameplay();
        var players = SpawnPlayersFromConfig();
        
        if (MatchManager.instance != null)
        {
            MatchManager.instance.InitializePlayers(players);
        }
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
