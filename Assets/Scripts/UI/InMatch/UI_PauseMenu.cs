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

        // Online the world keeps running (other players are still playing);
        // freezing time would stall the host's simulation for everyone.
        if (!GameSession.OnlineActive)
            Time.timeScale = 0f;
    }

    public void ResumeGame()
    {
        isPaused = false;
        mm.UnpauseMatch(); // returns players to Player input

        pausePanel.SetActive(false);
        Time.timeScale = 1f;
        EventSystem.current?.SetSelectedGameObject(null);
    }

    // ------- Buttons -------
    public void GoToCharacterSelect() { LeaveOrChangeScene("MatchMenu"); }
    public void GoToStageSelect()     { LeaveOrChangeScene("MapSelector"); }
    public void GoToMainMenu()        { LeaveOrChangeScene("MainMenu"); }

    private void LeaveOrChangeScene(string sceneName)
    {
        ForceResume();

        if (GameSession.OnlineActive)
        {
            // Leaving an online match disconnects (host leaving ends the session).
            ConnectionManager.Instance?.Leave();
            return;
        }

        GameManager.instance.ChangeScene(sceneName);
    }

    private void ForceResume()
    {
        Time.timeScale = 1f;
        mm.UnpauseMatch();
    }
}
