using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class UI_PauseMenu : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private Button btnCharacter;
    [SerializeField] private Button btnStage;
    [SerializeField] private Button btnMainMenu;
    [SerializeField] private EventSystem eventSystem;

    private bool isPaused = false;
    private MatchManager mm => MatchManager.instance;
    private PlayerInput uiInput; // <<--- receives UI pause input

    private void Awake()
    {
        uiInput = GetComponent<PlayerInput>(); // REQUIRE PlayerInput on same object
    }

    private void Start()
    {
        pausePanel.SetActive(false);

        btnCharacter?.onClick.AddListener(GoToCharacterSelect);
        btnStage?.onClick.AddListener(GoToStageSelect);
        btnMainMenu?.onClick.AddListener(GoToMainMenu);
    }

    public void TogglePause()
    {
        if (!mm.isMatchRunning) return;

        UI_MatchResults results = FindObjectOfType<UI_MatchResults>(true);
        if (results != null && results.gameObject.activeInHierarchy) return;

        if (isPaused) ResumeGame();
        else PauseGame();
    }

    private void PauseGame()
    {
        isPaused = true;
        mm.PauseMatch(); // switches players to UI input

        pausePanel.SetActive(true);
        if (eventSystem != null)
            eventSystem.enabled = true;


        btnStage?.Select();
        Time.timeScale = 0f;
    }

    public void ResumeGame()
    {
        isPaused = false;
        mm.UnpauseMatch(); // returns players to Player input

        pausePanel.SetActive(false);
        Time.timeScale = 1f;
    }

    // ------- Buttons -------
    public void GoToCharacterSelect() { ForceResume(); GameManager.instance.ChangeScene("MatchMenu"); }
    public void GoToStageSelect()     { ForceResume(); GameManager.instance.ChangeScene("MapSelector"); }
    public void GoToMainMenu()        { ForceResume(); GameManager.instance.ChangeScene("MainMenu"); }

    private void ForceResume()
    {
        Time.timeScale = 1f;
        mm.UnpauseMatch();
    }
}
