using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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
    [Tooltip("Se deshabilita mientras hay un pedido en curso, para que no se pueda mandar más de uno a la vez.")]
    [SerializeField] private Button connectButton;

    private bool isConnecting;

    public void ConnectButtonPressed()
    {
        if (isConnecting) return;

        if (string.IsNullOrWhiteSpace(brokerUrl))
        {
            SetStatus("Broker de GameLift no configurado.");
            return;
        }

        isConnecting = true;
        if (connectButton != null) connectButton.interactable = false;

        SetStatus("Solicitando sesión...");
        var provider = new GameLiftConnectionProvider(brokerUrl.Trim());
        StartCoroutine(provider.TryRequestConnectionAsync(null, OnConnectionResolved));
    }

    private void OnConnectionResolved(bool success, ConnectionInfo info, string error)
    {
        isConnecting = false;
        if (connectButton != null) connectButton.interactable = true;

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
