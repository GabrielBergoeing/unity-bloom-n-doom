using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class UI_OnlineDirectMenu : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private string onlineLobbySceneName = "OnlineLobby";

    [Header("Host")]
    [SerializeField] private TMP_InputField hostPortInput;
    [SerializeField] private ushort defaultHostPort = 7777;

    [Header("Join")]
    [SerializeField] private TMP_InputField joinIpInput;
    [SerializeField] private TMP_InputField joinPortInput;
    [SerializeField] private string defaultJoinIp = "127.0.0.1";
    [SerializeField] private ushort defaultJoinPort = 7777;

    public void HostButtonPressed()
    {
        ushort hostPort = ParsePort(hostPortInput, defaultHostPort);
        NetworkLaunchRequest.SetHost(hostPort);
        LoadOnlineLobby();
    }

    public void JoinButtonPressed()
    {
        string ip = ParseAddress(joinIpInput, defaultJoinIp);
        ushort port = ParsePort(joinPortInput, defaultJoinPort);

        NetworkLaunchRequest.SetJoin(ip, port);
        LoadOnlineLobby();
    }

    private void LoadOnlineLobby()
    {
        if (GameManager.instance != null)
        {
            GameManager.instance.ChangeScene(onlineLobbySceneName);
            return;
        }

        SceneManager.LoadScene(onlineLobbySceneName);
    }

    private static string ParseAddress(TMP_InputField input, string fallback)
    {
        string value = input == null ? string.Empty : input.text;
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static ushort ParsePort(TMP_InputField input, ushort fallback)
    {
        string value = input == null ? string.Empty : input.text;
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        if (ushort.TryParse(value.Trim(), out ushort parsed) && parsed > 0)
            return parsed;

        return fallback;
    }
}
