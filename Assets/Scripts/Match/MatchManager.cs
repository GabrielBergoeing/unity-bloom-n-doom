using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class MatchManager : MonoBehaviour
{
    public static MatchManager instance;
    private ScoreTally scoreTally;
    private LevelData currentLevel;
    private bool hasPrintResults = false;

    [Header("Match Time (in seconds)")]
    [SerializeField] private float matchTime;
    private bool isPlayingMatch = false; //Indicates when to start counting down timer
    public float timer { get; private set; }

    [Header("Player Spawn Locations")]
    [SerializeField] private Vector3[] playerSpawns;
    private List<PlayerInput> players = new();

    private void OnValidate() //Checks whenever values are changed on inspector
    {
        if (playerSpawns.Length > 4)
            Array.Resize(ref playerSpawns, 4);
    }

    private void Awake()
    {
        instance = this;
        scoreTally = GetComponent<ScoreTally>();

        currentLevel = GameManager.instance.currentLevel;
        timer = currentLevel.matchDuration;

        for(int i = 0; i < 4; i++)
            playerSpawns[i] = currentLevel.playerSpawnPositions[i];
    }

    private void Update()
    {
        if (isPlayingMatch)
            timer -= Time.deltaTime;

        if (timer <= 0 && !hasPrintResults)
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
        if (playerSpawns[index] == null)
            return new Vector3(index * 2f, 0f, 0f);
        return playerSpawns[index];
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
        scoreTally.DeterminePlacements(players);
        hasPrintResults = true;
    }
}