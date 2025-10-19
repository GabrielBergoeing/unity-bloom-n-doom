using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class MatchManager : MonoBehaviour
{
    private List<PlayerInput> players = new();

    public void InitializePlayers(List<PlayerInput> _players)
    {
        players = _players;
        SpawnPlayers();
    }

    private void SpawnPlayers()
    {
        if (players == null || players.Count == 0)
        {
            Debug.LogWarning("MatchManager: No players to spawn!");
            return;
        }

        for (int i = 0; i < players.Count; i++)
        {
            var p = players[i];
            p.transform.position = GetSpawnPosition(i);
            Debug.Log($"Player {i} positioned at {p.transform.position}");
        }
    }

    private Vector3 GetSpawnPosition(int index)
    {
        return new Vector3(index * 2f, 0f, 0f);
    }
}