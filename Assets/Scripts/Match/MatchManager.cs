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

    public bool isMatchRunning => isPlayingMatch && !hasPrintResults;

    [Header("Match Time (in seconds)")]
    [SerializeField] private float matchTime;
    private bool isPlayingMatch = false;
    public float timer { get; private set; }

    [Header("Player Spawn Locations")]
    [SerializeField] private Vector3[] playerSpawns;
    private List<PlayerInput> players = new();

    private void Awake()
    {
        instance = this;
        scoreTally = GetComponent<ScoreTally>();

        currentLevel = GameManager.instance.currentLevel;
        timer = currentLevel.matchDuration;

        for (int i = 0; i < 4; i++)
            playerSpawns[i] = currentLevel.playerSpawnPositions[i];
    }

    private void Update()
    {
        HandlePauseInput();

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
        }
    }

    private Vector3 GetSpawnPosition(int index)
    {
        return playerSpawns[index] == null
            ? new Vector3(index * 2f, 0f, 0f)
            : playerSpawns[index];
    }

    // --------------------------- PAUSE LOGIC --------------------------- //

    private void HandlePauseInput()
    {
        if (!isMatchRunning) return;

        foreach (var p in players)
        {
            if (p.actions["Pause"].triggered)
            {
                TogglePauseUI();
                return;
            }
        }
    }

    private void TogglePauseUI()
    {
        UI_PauseMenu pauseUI = FindObjectOfType<UI_PauseMenu>(true);
        if (pauseUI != null)
            pauseUI.TogglePause();
    }

    public void PauseMatch(bool pause)
    {
        isPlayingMatch = !pause;

        foreach (var p in players)
        {
            if (pause)
            {
                p.DeactivateInput();
                p.SwitchCurrentActionMap("UI");
            }
            else
            {
                p.ActivateInput();
                p.SwitchCurrentActionMap("Player");
            }
        }
    }

    public void PauseMatch() => PauseMatch(true);
    public void UnpauseMatch() => PauseMatch(false);

    // --------------------------- END MATCH --------------------------- //

    private void EndMatch()
    {
        PauseMatch(true); // freeze match

        List<ScoreResult> results = scoreTally.DeterminePlacements(players);
        Debug.Log(results);
        
        UI_MatchResults ui = FindObjectOfType<UI_MatchResults>(true);
        if (ui != null)
            ui.ShowResults(results);

        hasPrintResults = true;
    }
}
