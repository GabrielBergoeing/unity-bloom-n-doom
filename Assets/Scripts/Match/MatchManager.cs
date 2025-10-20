using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

public class MatchManager : MonoBehaviour
{
    public static MatchManager instance;
    [SerializeField] private float matchTime = 900; //15 min
    private bool isPlayingMatch = false; //Indicates when to start counting down timer
    public float timer { get; private set; }
    private List<PlayerInput> players = new();

    private void Awake()
    {
        instance = this;
        timer = matchTime;
    }

    private void Update()
    {
        if (isPlayingMatch)
            timer -= Time.deltaTime;

        if (timer <= 0)
            EndMatch();
    }
    public void InitializePlayers(List<PlayerInput> _players)
    {
        players = _players;
        SpawnPlayers();
        isPlayingMatch = true;
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

    private void DisablePlayersInputs()
    {
        foreach (var p in players)
        {
            if (p != null)
            {
                // p.SwitchCurrentActionMap("UI");
                p.DeactivateInput();
                Debug.Log($"[MatchManager] Disabled input for Player {p.playerIndex}");
            }
        }
    }

    //Necesito desactivar inputs de todos los jugadores
    private void EndMatch()
    {
        DisablePlayersInputs();
        Debug.Log("Match has ended!");
    }
}