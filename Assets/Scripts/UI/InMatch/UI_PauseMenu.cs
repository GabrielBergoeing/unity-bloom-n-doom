using Mirror;
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

    private bool IsMatchRunning()
    {
        if (OnlineMatchManager.instance != null) return OnlineMatchManager.instance.isMatchRunning;
        if (MatchManager.instance != null)       return MatchManager.instance.isMatchRunning;
        return false;
    }

    private void PauseActiveMatch()
    {
        if (OnlineMatchManager.instance != null) OnlineMatchManager.instance.PauseMatch();
        else MatchManager.instance?.PauseMatch();
    }

    private void UnpauseActiveMatch()
    {
        if (OnlineMatchManager.instance != null) OnlineMatchManager.instance.UnpauseMatch();
        else MatchManager.instance?.UnpauseMatch();
    }
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
        if (!IsMatchRunning()) return;

        UI_MatchResults results = FindObjectOfType<UI_MatchResults>(true);
        if (results != null && results.gameObject.activeInHierarchy) return;

        if (isPaused) ResumeGame();
        else PauseGame();
    }

    private void PauseGame()
    {
        isPaused = true;
        PauseActiveMatch();

        pausePanel.SetActive(true);
        if (eventSystem != null)
            eventSystem.enabled = true;


        btnStage?.Select();
        Time.timeScale = 0f;
    }

    public void ResumeGame()
    {
        isPaused = false;
        UnpauseActiveMatch();

        pausePanel.SetActive(false);
        Time.timeScale = 1f;
    }

    // ------- Buttons -------
    private bool IsOnline => OnlineMatchManager.instance != null;

    public void GoToCharacterSelect()
    {
        ForceResume();

        if (IsOnline)
        {
            if (NetworkServer.active)
                NetworkManager.singleton.ServerChangeScene("CharacterSelectorOnline");
            else
                Debug.Log("[UI_PauseMenu] Only the host can return to character select.");
            return;
        }

        GameManager.instance.ChangeScene("MatchMenu");
    }

    public void GoToStageSelect()
    {
        ForceResume();

        if (IsOnline)
        {
            if (NetworkServer.active)
                NetworkManager.singleton.ServerChangeScene("MapSelectorOnline");
            else
                Debug.Log("[UI_PauseMenu] Only the host can return to stage select.");
            return;
        }

        GameManager.instance.ChangeScene("MapSelector");
    }

    public void GoToMainMenu()
    {
        ForceResume();

        if (IsOnline)
        {
            // OnlineNetworkManager.OnClientDisconnect navigates to MainMenu once stopped.
            if (NetworkServer.active)
                NetworkManager.singleton.StopHost();
            else
                NetworkManager.singleton.StopClient();
            return;
        }

        GameManager.instance.ChangeScene("MainMenu");
    }

    private void ForceResume()
    {
        Time.timeScale = 1f;
        UnpauseActiveMatch();
    }
}
