using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Handles hosting / joining by IP + port (Unity Transport), connection approval,
/// and returning everyone to the main menu when the session ends.
/// Lives on the NetworkBootstrap prefab next to the NetworkManager.
/// </summary>
public class ConnectionManager : MonoBehaviour
{
    public static ConnectionManager Instance { get; private set; }

    public const int MaxPlayers = 4;
    public const string MenuSceneName = "MainMenu";

    public enum TransportKind { UnityTransport, Personalized, Kcp }

    [Header("Transport (must match on host and clients)")]
    public TransportKind transport = TransportKind.UnityTransport;

    public string StatusMessage { get; private set; } = "";
    public bool IsConnecting { get; private set; }

    /// <summary>Endpoint of the current session, for telemetry ("host:port" or "ip:port").</summary>
    public string LastAddress { get; private set; } = "(no address)";

    private bool prefabsRegistered;
    private bool pendingMenuFade;

    private NetworkManager Nm => NetworkManager.Singleton;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        Nm.ConnectionApprovalCallback = OnConnectionApproval;
        Nm.OnConnectionEvent += OnConnectionEvent;
        Nm.OnClientStopped += OnLocalStopped;
        Nm.OnServerStopped += OnLocalStopped;
        Nm.OnTransportFailure += OnTransportFailure;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Nm == null) return;
        Nm.OnConnectionEvent -= OnConnectionEvent;
        Nm.OnClientStopped -= OnLocalStopped;
        Nm.OnServerStopped -= OnLocalStopped;
        Nm.OnTransportFailure -= OnTransportFailure;
    }

    /// <summary>
    /// Scenes start behind an opaque UI_FadeScreen; offline, GameManager's scene
    /// transition fades it in, but networked loads (NetworkSceneManager) and the
    /// post-disconnect return to menu bypass GameManager — fade in here instead.
    /// </summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;
        if (!GameSession.OnlineActive && !pendingMenuFade) return;
        pendingMenuFade = false;

        var fade = UIService.instance != null && UIService.instance.fade != null
            ? UIService.instance.fade
            : FindFirstObjectByType<UI_FadeScreen>();
        if (fade != null)
            fade.FadeIn();
    }

    // ======================================================
    //  HOST / JOIN / LEAVE
    // ======================================================
    public bool StartHost(ushort port)
    {
        if (Nm.IsListening) return false;

        RegisterNetworkPrefabs();

        if (!ConfigureTransport("127.0.0.1", port, isServer: true))
            return false;

        Nm.NetworkConfig.ConnectionApproval = true;

        if (!Nm.StartHost())
        {
            StatusMessage = $"Failed to start host on port {port} (port in use?)";
            return false;
        }

        SpawnSession();
        OnOnlineSessionStarted();
        LastAddress = $"host:{port}";
        NetTrafficCounter.Reset();
        StatusMessage = $"Hosting on port {port}. Others join with your IP (port-forward {port}/UDP).";
        return true;
    }

    public bool StartClient(string ip, ushort port)
    {
        if (Nm.IsListening) return false;

        RegisterNetworkPrefabs();

        if (!ConfigureTransport(ip, port, isServer: false))
            return false;

        Nm.NetworkConfig.ConnectionApproval = true;

        if (!Nm.StartClient())
        {
            StatusMessage = "Failed to start client.";
            return false;
        }

        IsConnecting = true;
        OnOnlineSessionStarted();
        LastAddress = $"{ip}:{port}";
        NetTrafficCounter.Reset();
        StatusMessage = $"Connecting to {ip}:{port} ...";
        return true;
    }

    public void Leave()
    {
        if (Nm != null && Nm.IsListening)
            Nm.Shutdown();
    }

    /// <summary>
    /// Points NetworkConfig at the selected transport component and configures its
    /// address/port. Both peers must select the same transport in the overlay.
    /// </summary>
    private bool ConfigureTransport(string ip, ushort port, bool isServer)
    {
        if (transport == TransportKind.Personalized)
        {
            var custom = Nm.GetComponent<PersonalizedTransport>();
            if (custom == null)
            {
                StatusMessage = "PersonalizedTransport missing on the bootstrap prefab. Re-run Tools > NGO Setup.";
                return false;
            }
            custom.address = ip;
            custom.port = port;
            Nm.NetworkConfig.NetworkTransport = custom;
            return true;
        }

        if (transport == TransportKind.Kcp)
        {
            var kcp = Nm.GetComponent<KcpNgoTransport>();
            if (kcp == null)
            {
                StatusMessage = "KcpNgoTransport missing on the bootstrap prefab. Re-run Tools > NGO Setup.";
                return false;
            }
            kcp.address = ip;
            kcp.port = port;
            Nm.NetworkConfig.NetworkTransport = kcp;
            return true;
        }

        var utp = Nm.GetComponent<UnityTransport>();
        if (utp == null)
        {
            StatusMessage = "UnityTransport missing on the bootstrap prefab. Re-run Tools > NGO Setup.";
            return false;
        }
        if (isServer)
            utp.SetConnectionData("127.0.0.1", port, "0.0.0.0"); // listen on all interfaces
        else
            utp.SetConnectionData(ip, port);
        Nm.NetworkConfig.NetworkTransport = utp;
        return true;
    }

    // ======================================================
    //  CALLBACKS
    // ======================================================
    private void OnConnectionApproval(NetworkManager.ConnectionApprovalRequest request,
                                      NetworkManager.ConnectionApprovalResponse response)
    {
        response.CreatePlayerObject = false; // players are spawned manually when the match starts
        response.Approved = true;

        // The host approves itself before the session exists.
        if (request.ClientNetworkId == Nm.LocalClientId)
            return;

        if (GameSession.Instance == null ||
            GameSession.Instance.State != GameSession.SessionState.Lobby)
        {
            response.Approved = false;
            response.Reason = "Match already in progress.";
            return;
        }

        if (Nm.ConnectedClientsIds.Count >= MaxPlayers)
        {
            response.Approved = false;
            response.Reason = "Lobby is full.";
        }
    }

    private void OnConnectionEvent(NetworkManager nm, ConnectionEventData data)
    {
        if (data.EventType == ConnectionEvent.ClientConnected &&
            data.ClientId == nm.LocalClientId)
        {
            IsConnecting = false;
            StatusMessage = nm.IsHost ? StatusMessage : "Connected!";
        }
    }

    private void OnLocalStopped(bool wasHost)
    {
        IsConnecting = false;
        if (string.IsNullOrEmpty(StatusMessage) || !StatusMessage.StartsWith("Disconnected"))
        {
            string reason = Nm != null ? Nm.DisconnectReason : "";
            StatusMessage = string.IsNullOrEmpty(reason) ? "Disconnected." : $"Disconnected: {reason}";
        }
        ReturnToMenu();
    }

    private void OnTransportFailure()
    {
        StatusMessage = "Network transport failure.";
    }

    // ======================================================
    //  HELPERS
    // ======================================================
    private void SpawnSession()
    {
        var prefab = NetworkAssets.Instance != null ? NetworkAssets.Instance.sessionPrefab : null;
        if (prefab == null)
        {
            Debug.LogError("[ConnectionManager] NetworkAssets.sessionPrefab missing. Run the NGO setup wizard.");
            return;
        }
        var go = Instantiate(prefab);
        go.GetComponent<NetworkObject>().Spawn();
    }

    private void RegisterNetworkPrefabs()
    {
        if (prefabsRegistered) return;

        var assets = NetworkAssets.Instance;
        if (assets == null)
        {
            Debug.LogError("[ConnectionManager] Missing Resources/NetworkAssets. Run the NGO setup wizard.");
            return;
        }

        TryRegister(assets.sessionPrefab);
        foreach (var c in assets.Characters)
            if (c != null) TryRegister(c.prefab);
        foreach (var p in assets.plants) TryRegister(p);
        foreach (var i in assets.items) TryRegister(i);

        prefabsRegistered = true;
    }

    private void TryRegister(GameObject prefab)
    {
        if (prefab == null) return;
        if (prefab.GetComponent<NetworkObject>() == null)
        {
            Debug.LogError($"[ConnectionManager] Prefab '{prefab.name}' has no NetworkObject. Run the NGO setup wizard.");
            return;
        }
        try { Nm.AddNetworkPrefab(prefab); }
        catch { /* already registered */ }
    }

    private void OnOnlineSessionStarted()
    {
        // Local-multiplayer device joining makes no sense while online.
        if (PlayerInputManager.instance != null)
            PlayerInputManager.instance.DisableJoining();
    }

    private void ReturnToMenu()
    {
        Time.timeScale = 1f;

        if (PlayerInputManager.instance != null)
            PlayerInputManager.instance.EnableJoining();

        if (SceneManager.GetActiveScene().name != MenuSceneName)
        {
            pendingMenuFade = true; // menu also starts behind the fade overlay
            SceneManager.LoadScene(MenuSceneName);
        }
    }
}
