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

        CharacterData data = ResolveCharacterData(winner.playerIndex);
        if (data == null)
        {
            Debug.LogError($"[UI_MatchResults] Could not resolve character for winner slot {winner.playerIndex}!");
            return;
        }

        imgPortrait.sprite = data.portrait;
        txtScore.text = winner.score + " pts";
        txtPlayer.text = $"Player {winner.playerIndex + 1}";

        resultsPanel.SetActive(true);
        if (eventSystem != null) eventSystem.enabled = true;

        btnReturnCharacter?.Select();
    }

    // winner.playerIndex is the player's slot index, not a CharacterDatabase index -
    // players can pick any character in any slot, so look up what was actually
    // selected for that slot instead of assuming characterDB order matches slot order.
    private CharacterData ResolveCharacterData(int slotIndex)
    {
        var service = PlayerInputService.instance;
        if (service != null)
        {
            var online = service.OnlineSelectedCharacters;
            if (online != null && slotIndex >= 0 && slotIndex < online.Count && online[slotIndex] != null)
                return online[slotIndex];

            var config = service.GetConfig(slotIndex);
            if (config?.selectedCharacter != null)
                return config.selectedCharacter;
        }

        // Fallback for setups with no recorded selection: assume characterDB order matches slot order.
        if (characterDB != null && slotIndex >= 0 && slotIndex < characterDB.characters.Length)
            return characterDB.characters[slotIndex];

        return null;
    }
    #endregion

    #region Buttons
    public void GoToCharacterSelect()
    {
        UI.sfx.PlayOnConfirm();
        AudioManager.instance.StartBGM("bgm_menu");
        GameManager.instance.ChangeScene("MatchMenu");
    }

    public void GoToStageSelect()
    {
        UI.sfx.PlayOnConfirm();
        AudioManager.instance.StartBGM("bgm_menu");
        GameManager.instance.ChangeScene("MapSelector");
    }

    public void GoToMainMenu()
    {
        UI.sfx.PlayOnConfirm();
        AudioManager.instance.StartBGM("bgm_menu");
        GameManager.instance.ChangeScene("MainMenu");
    }
    #endregion
}
