using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

// Third way to connect online, alongside UI_OnlineDirectMenu's P2P join-code flow.
// Self-contained: doesn't touch UI_OnlineDirectMenu or SteamLobby. Drop this on a
// GameObject with a "Conectar" button wired to ConnectButtonPressed() and fill in
// brokerUrl once Tools/GameLiftBroker is deployed (see its README) - left blank by
// default so this is a safe no-op until configured.
public class UI_GameLiftMenu : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private string onlineLobbySceneName = "CharacterSelectorOnline";

    [Header("Broker (Tools/GameLiftBroker)")]
    [Tooltip("URL del broker, ej. http://mi-broker.example.com:8090. Vacío = deshabilitado.")]
    [SerializeField] private string brokerUrl = "";

    [Header("UI (opcional)")]
    [SerializeField] private TMP_Text statusText;

    public void ConnectButtonPressed()
    {
        if (string.IsNullOrWhiteSpace(brokerUrl))
        {
            SetStatus("Broker de GameLift no configurado.");
            return;
        }

        SetStatus("Solicitando sesión...");
        var provider = new GameLiftConnectionProvider(brokerUrl.Trim());
        StartCoroutine(provider.TryRequestConnectionAsync(null, OnConnectionResolved));
    }

    private void OnConnectionResolved(bool success, ConnectionInfo info, string error)
    {
        if (!success)
        {
            SetStatus($"Error: {error}");
            return;
        }

        SetStatus("Conectando...");
        NetworkLaunchRequest.SetJoin(info.address, info.port, sessionToken: info.sessionToken);

        if (GameManager.instance != null)
            GameManager.instance.ChangeScene(onlineLobbySceneName);
        else
            SceneManager.LoadScene(onlineLobbySceneName);
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;

        Debug.Log($"[UI_GameLiftMenu] {message}");
    }
}
