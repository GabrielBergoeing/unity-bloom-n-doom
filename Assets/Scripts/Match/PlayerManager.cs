using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerManager : MonoBehaviour
{
    [SerializeField] private GameObject playerPrefab;
    private List<PlayerInput> activePlayers = new();

    private void Start()
    {
        var configs = GameManager.instance.playerConfigs;
        Debug.Log($"[PlayerManager] Found {configs?.Count} configs in GameManager");
        SpawnPlayersFromConfig();
    }

    private void SpawnPlayersFromConfig()
    {
        var configs = GameManager.instance.playerConfigs;
        if (configs == null || configs.Count == 0)
        {
            Debug.LogWarning("No player configurations found in GameManager!");
            return;
        }

        for (int i = 0; i < configs.Count; i++)
        {
            var cfg = configs[i];
            var player = PlayerInput.Instantiate(playerPrefab, i, cfg.controlScheme, -1, cfg.device);
            player.transform.position = Vector3.zero;
            activePlayers.Add(player);

            Debug.Log($"Spawned player {i} with {cfg.controlScheme} ({cfg.device.displayName})");
        }

        // Hand off to MatchManager if it exists
        var matchManager = FindFirstObjectByType<MatchManager>();
        if (matchManager != null)
            matchManager.InitializePlayers(activePlayers);
    }
}
