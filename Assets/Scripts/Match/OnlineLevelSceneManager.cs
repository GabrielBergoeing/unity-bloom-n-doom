using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Replaces GameSceneLoader in the online level scene (LevelSceneOnline).
/// Mirror spawns players via OnlineNetworkManager.OnServerAddPlayer —
/// this component just starts the MatchManager timer once the scene is ready.
///
/// Place this on any persistent GameObject in LevelSceneOnline.
/// Do NOT put GameSceneLoader in the online level scene.
/// </summary>
public class OnlineLevelSceneManager : MonoBehaviour
{
    private void Start()
    {
        if (!NetworkClient.active && !NetworkServer.active)
        {
            Debug.LogWarning("[OnlineLevelSceneManager] No active Mirror session. Skipping online init.");
            return;
        }

        // Mirror handles spawning via OnServerAddPlayer/OnClientSceneChanged.
        // We just need to start the match timer.
        // Pass an empty PlayerInput list: MatchManager starts the timer without
        // moving or enabling any local PlayerInput objects (Player handles that
        // itself via OnStartLocalPlayer).
        if (MatchManager.instance != null)
        {
            MatchManager.instance.InitializePlayers(new List<PlayerInput>());
            Debug.Log("[OnlineLevelSceneManager] Match timer started.");
        }
        else
        {
            Debug.LogWarning("[OnlineLevelSceneManager] MatchManager not found in scene.");
        }
    }
}
