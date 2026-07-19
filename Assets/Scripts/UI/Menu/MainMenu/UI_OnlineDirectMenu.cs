using System.Collections;
using System.Net;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class UI_OnlineDirectMenu : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private string onlineLobbySceneName = "CharacterSelectorOnline";

    [Header("Host")]
    [Tooltip("Puerto en el que se hostea. No hace falta que el jugador lo escriba - va incluido en el código de sala.")]
    [FormerlySerializedAs("defaultHostPort")]
    [SerializeField] private ushort hostPort = 7777;
    [Tooltip("Muestra el código de sala generado para compartir con amigos (se autocompleta al abrir el menú).")]
    [FormerlySerializedAs("hostPortInput")]
    [SerializeField] private TMP_InputField hostCodeDisplay;
    [Tooltip("Opcional: forzar una IP manual en vez de autodetectar la pública (útil para pruebas locales/loopback).")]
    [SerializeField] private TMP_InputField hostAddressOverrideInput;

    [Header("Join")]
    [Tooltip("Código de sala compartido por el host.")]
    [FormerlySerializedAs("joinIpInput")]
    [SerializeField] private TMP_InputField joinCodeInput;

    [Header("Autodetección de IP pública")]
    [SerializeField] private string publicIpLookupUrl = "https://api.ipify.org";
    [SerializeField] private float publicIpTimeoutSeconds = 4f;

    private readonly IConnectionProvider connectionProvider = new JoinCodeConnectionProvider();
    private string detectedLocalAddress;
    private string detectedPublicAddress;

    private void Start()
    {
        if (hostCodeDisplay != null)
            hostCodeDisplay.readOnly = true;

        RefreshHostCode();

        // Note: a gamepad user can navigate to and "press" joinCodeInput to enter edit
        // mode, but there is currently no on-screen keyboard, so they still can't type
        // actual characters into it - only a real keyboard can fill in a room code today.
        var first = GetComponentInChildren<Selectable>(true);
        if (first != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(first.gameObject);
    }

    public void HostButtonPressed()
    {
        // The join code doubles as the room id for HolePunchClient's signaling
        // server fallback - reusing it means there's still only one code to share.
        string roomCode = hostCodeDisplay != null ? hostCodeDisplay.text : null;
        NetworkLaunchRequest.SetHost(hostPort, roomCode);
        LoadOnlineLobby();
    }

    public void JoinButtonPressed()
    {
        string code = joinCodeInput != null ? joinCodeInput.text : string.Empty;

        if (!connectionProvider.TryRequestConnection(code, out ConnectionInfo info, out string error))
        {
            Debug.LogWarning($"[UI_OnlineDirectMenu] Código de sala inválido: {error}");
            return;
        }

        NetworkLaunchRequest.SetJoin(info.address, info.port, info.fallbackAddress, roomCode: code, sessionToken: info.sessionToken);
        LoadOnlineLobby();
    }

    // Wire this to a "Refresh" button if a manual IP override is added later; also
    // called once from Start() to populate the host code with no extra UI needed.
    public void RefreshHostCode()
    {
        StartCoroutine(DetectHostAddressAndShowCode());
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

    private IEnumerator DetectHostAddressAndShowCode()
    {
        string manualOverride = hostAddressOverrideInput != null ? hostAddressOverrideInput.text.Trim() : string.Empty;
        if (!string.IsNullOrEmpty(manualOverride))
        {
            detectedLocalAddress = manualOverride;
            detectedPublicAddress = manualOverride;
            ShowHostCode();
            yield break;
        }

        // In-Editor testing (single instance or ParrelSync clones) always runs on the
        // same physical machine, and most home routers don't support NAT hairpinning
        // (routing your own public IP back to yourself) - a public-IP code would just
        // time out. Real builds still need the actual public IP for friends over the
        // internet, so this only short-circuits in the Editor.
        if (Application.isEditor)
        {
            detectedLocalAddress = "127.0.0.1";
            detectedPublicAddress = "127.0.0.1";
            ShowHostCode();
            yield break;
        }

        // The LAN address is tried first by the client (see SteamLobby.JoinWithFallback) -
        // it covers same-network testing/LAN parties without depending on NAT/UPnP at all.
        detectedLocalAddress = NetworkAddressUtil.GetLocalIPv4();

        using (UnityWebRequest request = UnityWebRequest.Get(publicIpLookupUrl))
        {
            request.timeout = Mathf.CeilToInt(publicIpTimeoutSeconds);
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string text = request.downloadHandler.text.Trim();
                if (IPAddress.TryParse(text, out _))
                    detectedPublicAddress = text;
            }
        }

        if (string.IsNullOrEmpty(detectedPublicAddress))
            detectedPublicAddress = detectedLocalAddress ?? "127.0.0.1";

        ShowHostCode();
    }

    private void ShowHostCode()
    {
        if (hostCodeDisplay == null)
            return;

        // 0.0.0.0 tells JoinCode/JoinCodeConnectionProvider "no LAN candidate" - the
        // client then just uses the public address directly instead of wasting time on
        // a guaranteed-bad attempt.
        IPAddress local = IPAddress.TryParse(detectedLocalAddress ?? "", out IPAddress parsedLocal)
            ? parsedLocal
            : IPAddress.Any;

        if (!IPAddress.TryParse(detectedPublicAddress ?? "", out IPAddress publicAddress))
        {
            hostCodeDisplay.text = "No se pudo detectar una IP.";
            return;
        }

        // SteamLobby.HostDirect() attempts to open the port automatically via UPnP
        // (best-effort - see UpnpPortMapper) for when the public address is needed. On
        // routers without UPnP or behind CGNAT that still fails silently and the host
        // needs manual forwarding; that requirement goes away entirely once GameLift
        // dedicated servers are adopted.
        hostCodeDisplay.text = JoinCode.Encode(local, publicAddress, hostPort);
    }
}
