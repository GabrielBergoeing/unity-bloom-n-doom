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
    [SerializeField] private Transform resultsContainer;
    [SerializeField] private CharacterDatabase characterDB;

    [Header("Row Template (Disabled)")]
    [SerializeField] private GameObject resultRowTemplate;

    [Header("Menu Buttons")]
    [SerializeField] private Button btnReturnCharacter;
    [SerializeField] private Button btnReturnStage;
    [SerializeField] private Button btnReturnMainMenu;

    private bool initialized = false;

    private void Awake()
    {
        resultsPanel.SetActive(false);
        resultRowTemplate.SetActive(false);

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

        // Sort by score DESC (Higher wins)
        results.Sort((a, b) => b.score.CompareTo(a.score));

        // Clear old
        foreach (Transform child in resultsContainer)
            if (child != resultRowTemplate.transform)
                Destroy(child.gameObject);

        // Build rows
        foreach (var r in results)
        {
            CharacterData data = characterDB.characters[r.playerIndex];
            var entry = CreateResultRow(data, r.score);
            entry.SetActive(true);
        }

        // Show panel
        resultsPanel.SetActive(true);
        if (eventSystem != null) eventSystem.enabled = true;

        btnReturnCharacter?.Select();
    }

    private GameObject CreateResultRow(CharacterData data, int score)
    {
        var row = Instantiate(resultRowTemplate, resultsContainer);

        row.transform.Find("CharacterPortrait").GetComponent<Image>().sprite = data.portrait;
        row.transform.Find("CharacterName").GetComponent<TextMeshProUGUI>().text = data.characterName;
        row.transform.Find("ScoreLabel").GetComponent<TextMeshProUGUI>().text = score + " pts";

        return row;
    }
    #endregion

    #region Buttons
    public void GoToCharacterSelect()
    {
        UI.sfx.PlayOnConfirm();
        GameManager.instance.ChangeScene("MatchMenu");
    }

    public void GoToStageSelect()
    {
        UI.sfx.PlayOnConfirm();
        GameManager.instance.ChangeScene("MapSelector");
    }

    public void GoToMainMenu()
    {
        UI.sfx.PlayOnConfirm();
        GameManager.instance.ChangeScene("MainMenu");
    }
    #endregion
}
