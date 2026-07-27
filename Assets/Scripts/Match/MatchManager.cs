using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Scene-side match facade. Online, the authoritative timer/state lives on
/// GameSession (server); this class mirrors it for the local UI, handles the
/// local pause menu, and shows results when the server broadcasts them.
/// The legacy local-multiplayer path is kept for offline test scenes.
/// </summary>
public class MatchManager : MonoBehaviour
{
    public static MatchManager instance;
    private ScoreTally scoreTally;
    private LevelData currentLevel;
    private bool hasPrintResults = false;

    public bool isMatchRunning => GameSession.OnlineActive
        ? GameSession.Instance != null && GameSession.Instance.State == GameSession.SessionState.Playing
        : isPlayingMatch && !hasPrintResults;

    public float CurrentLevelDuration => currentLevel != null ? currentLevel.matchDuration : 300f;

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

        currentLevel = GameManager.instance != null ? GameManager.instance.currentLevel : null;
        if (currentLevel != null)
        {
            timer = currentLevel.matchDuration;

            for (int i = 0; i < 4 && i < currentLevel.playerSpawnPositions.Length; i++)
                playerSpawns[i] = currentLevel.playerSpawnPositions[i];
        }
    }

    private void Update()
    {
        HandlePauseInput();

        if (GameSession.OnlineActive)
        {
            // The server owns the timer and ends the match; we just mirror it.
            if (GameSession.Instance != null)
                timer = GameSession.Instance.matchTimer.Value;
            return;
        }

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

        if (GameSession.OnlineActive)
        {
            var local = NetworkPlayer.LocalPlayer;
            var pi = local != null ? local.player.input : null;
            if (pi != null && pi.isActiveAndEnabled && pi.actions["Pause"].triggered)
                TogglePauseUI();
            return;
        }

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
        if (GameSession.OnlineActive)
        {
            // Online there is no global pause; only the local player's input is remapped.
            var local = NetworkPlayer.LocalPlayer;
            var pi = local != null ? local.player.input : null;
            if (pi == null) return;

            if (pause)
                pi.SwitchCurrentActionMap("UI");
            else
                pi.SwitchCurrentActionMap("Player");
            return;
        }

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

    /// <summary>Called on every client when the server broadcasts the final scores.</summary>
    public void ShowResultsLocal(List<ScoreResult> results)
    {
        if (hasPrintResults) return;
        hasPrintResults = true;

        PauseMatch(true); // freeze local input

        UI_MatchResults ui = FindObjectOfType<UI_MatchResults>(true);
        if (ui != null)
            ui.ShowResults(results);
    }

    private void EndMatch()
    {
        PauseMatch(true); // freeze match

        List<ScoreResult> results = scoreTally.DeterminePlacements(players);

        UI_MatchResults ui = FindObjectOfType<UI_MatchResults>(true);
        if (ui != null)
            ui.ShowResults(results);

        hasPrintResults = true;
    }
}
