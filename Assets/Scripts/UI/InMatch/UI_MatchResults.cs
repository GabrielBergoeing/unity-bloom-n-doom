using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class UI_MatchResults : MonoBehaviour
{
    private UIService UI => UIService.instance;

    [Header("References")]
    [SerializeField] private GameObject resultsPanel;
    [SerializeField] private EventSystem eventSystem;
    [SerializeField] private CharacterDatabase characterDB;

    [Header("Winner UI")]
    [SerializeField] private Image imgPortrait;
    [SerializeField] private TextMeshProUGUI txtScore;
    [SerializeField] private TextMeshProUGUI txtPlayer;

    [Header("Menu Buttons")]
    [SerializeField] private Button btnReturnCharacter;
    [SerializeField] private Button btnReturnStage;
    [SerializeField] private Button btnReturnMainMenu;

    private bool initialized = false;

    private void Awake()
    {
        resultsPanel.SetActive(false);
        if (eventSystem != null) eventSystem.enabled = false;

        ValidateButton(btnReturnCharacter, "Character Select");
        ValidateButton(btnReturnStage, "Stage Select");
        ValidateButton(btnReturnMainMenu, "Main Menu");
    }

    private void ValidateButton(Button btn, string name)
    {
        if (btn == null)
            Debug.LogWarning($"[UI_MatchResults] Missing button reference: {name}");
    }

    #region Show Results
    public void ShowResults(List<ScoreResult> results)
    {
        if (initialized) return;
        initialized = true;

        if (results == null || results.Count == 0)
        {
            Debug.LogError("[UI_MatchResults] Tried to show results with an empty score list!");
            return;
        }

        // Sort
        results.Sort((a, b) => b.score.CompareTo(a.score));
        var winner = results[0];

        // Portrait comes from the character the winner picked
        int charIndex = Mathf.Clamp(winner.characterId, 0, characterDB.characters.Length - 1);
        CharacterData data = characterDB.characters[charIndex];

        imgPortrait.sprite = data.portrait;
        txtScore.text = winner.score + " pts";
        txtPlayer.text = $"Player {winner.playerIndex + 1}";

        resultsPanel.SetActive(true);
        if (eventSystem != null) eventSystem.enabled = true;

        btnReturnCharacter?.Select();
    }
    #endregion

    #region Buttons
    public void GoToCharacterSelect() => LeaveOrChangeScene("MatchMenu");

    public void GoToStageSelect() => LeaveOrChangeScene("MapSelector");

    public void GoToMainMenu() => LeaveOrChangeScene("MainMenu");

    private void LeaveOrChangeScene(string sceneName)
    {
        UI.sfx.PlayOnConfirm();
        AudioManager.instance.StartBGM("bgm_menu");

        if (GameSession.OnlineActive)
        {
            // Online the session ends here; everyone returns to the main menu.
            ConnectionManager.Instance?.Leave();
            return;
        }

        GameManager.instance.ChangeScene(sceneName);
    }
    #endregion
}
