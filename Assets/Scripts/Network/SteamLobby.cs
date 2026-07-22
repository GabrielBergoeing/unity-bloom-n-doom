using UnityEngine;
using Mirror;
using Steamworks;
using System;
using System.Collections;
using System.Net;
using System.Reflection;

public class SteamLobby : MonoBehaviour
{
    public enum NetworkMode
    {
        Steam,      // Usa FizzySteamworks + lobbies de Steam
        Host,     // Host directo con el transporte elegido (sin Steam)
        Join     // Join directo a remoteHostAddress con el transporte elegido
    }

    [Header("Modo de red")]
    [Tooltip("Elige cómo se comporta esta escena al entrar. Funciona igual en editor y en build.")]
    [SerializeField] private NetworkMode networkMode = NetworkMode.Host;

    [Header("Transporte")]
    [Tooltip("Transporte que se activará al iniciar. Si es null, no se toca el transporte activo.")]
    [SerializeField] private Transport overrideTransport;

    [Header("Conexión directa (DirectHost / DirectJoin / AutoByClone)")]
    [Tooltip("IP del host cuando esta instancia actúa como cliente.")]
    [SerializeField] private string remoteHostAddress = "127.0.0.1";
    [Tooltip("Segundos de espera antes de intentar conectar como cliente.")]
    [SerializeField] private float joinDelay = 1.5f;
    [Tooltip("Dirección alternativa (IP pública) a probar si la primera (LAN) falla - ver JoinDirect.")]
    [SerializeField] private string fallbackHostAddress;
    [Tooltip("Segundos a esperar por una dirección antes de probar la de respaldo.")]
    [SerializeField] private float directConnectTimeout = 4f;

    [Header("NAT / UPnP")]
    [Tooltip("Al hostear directo, intenta abrir el puerto automáticamente en el router (UPnP). Best-effort: no todos los routers lo soportan.")]
    [SerializeField] private bool autoPortForward = true;
    private ushort lastMappedPort;

    // -------------------------------------------------------
    private NetworkManager networkManager;
    private OnlineNetworkManager onlineNetworkManager; // for ClientConnectedOnline/ClientDisconnectedOnline, see JoinWithFallback
    private HolePunchClient holePunchClient; // third fallback tier, see JoinWithFallback / HostDirect
    private string roomCode; // shared join code, reused as the hole-punch signaling room id

    // Mirrors roomCode as a static so scripts on other GameObjects (NetworkMetrics on the
    // Player prefab, in particular) can tag their data with the match's room code without
    // needing a direct reference to this instance.
    public static string ActiveRoomCode { get; private set; }
    private ushort gamePort = 7777;
    private bool preventHosting = false;

    // Lets any UI (see UI_ConnectionStatus) show what the LAN -> public IP -> hole punch
    // cascade is doing right now, instead of going silent for 15+ seconds with nothing
    // on screen - that silence is what's been causing testers to assume it's frozen and
    // close the app mid-attempt, or mash the Backspace-to-menu shortcut out of impatience.
    public static event Action<string> OnConnectStatusChanged;
    private static void SetStatus(string message) => OnConnectStatusChanged?.Invoke(message);

    protected Callback<LobbyCreated_t> lobbyCreated;
    protected Callback<GameLobbyJoinRequested_t> gameLobbyJoinRequested;
    protected Callback<LobbyEnter_t> lobbyEntered;

    private const string HostAddressKey = "HostAddress";

    // -------------------------------------------------------
    void Start()
    {
#if UNITY_SERVER
        // A dedicated GameLift server has no player-driven launch request and this
        // component's networkMode defaults to Host - without this guard it would call
        // HostDirect() and spin up its own local host player before any real player ever
        // connects via GameLift, on top of the server GameLiftServerManager already starts
        // once a GameSession activates. GameLiftServerManager.Awake() is the sole owner of
        // starting the Mirror server for this build target.
        return;
#endif
        networkManager = GetComponent<NetworkManager>();
        if (networkManager == null)
        {
            Debug.LogError("[SteamLobby] No se encontró NetworkManager en el mismo GameObject.");
            return;
        }
        onlineNetworkManager = networkManager as OnlineNetworkManager;
        holePunchClient = GetComponent<HolePunchClient>();

        ApplyTransportOverride();
        ApplyRuntimeLaunchRequest();

        Debug.Log($"[SteamLobby] Start - mode={networkMode}, transport={Transport.active?.GetType().Name ?? "null"}");

        if (Application.dataPath.ToLower().Contains("clone"))
                {
                    networkMode = NetworkMode.Join;
                }

        switch (networkMode)
        {
            case NetworkMode.Steam:
                StartSteam();
                break;

            case NetworkMode.Host:
                Debug.Log("[SteamLobby] Modo Host.");
                HostDirect();
                break;

            case NetworkMode.Join:
                preventHosting = true;
                Debug.Log($"[SteamLobby] Modo Join → {remoteHostAddress}");
                Invoke(nameof(JoinDirect), joinDelay);
                break;
        }
    }

    private void ApplyRuntimeLaunchRequest()
    {
        if (!NetworkLaunchRequest.TryConsume(out NetworkLaunchRequest.LaunchData launchData))
            return;

        switch (launchData.mode)
        {
            case NetworkLaunchRequest.LaunchMode.Host:
                networkMode = NetworkMode.Host;
                break;

            case NetworkLaunchRequest.LaunchMode.Join:
                networkMode = NetworkMode.Join;
                remoteHostAddress = string.IsNullOrWhiteSpace(launchData.address) ? remoteHostAddress : launchData.address;
                fallbackHostAddress = launchData.fallbackAddress;
                break;

            default:
                return;
        }

        roomCode = launchData.roomCode;
        ActiveRoomCode = roomCode;
        if (launchData.port > 0)
            gamePort = launchData.port;

        // Forward the session token (if any) to the GameLift authenticator so a future
        // GameLiftConnectionProvider can be validated server-side without any other
        // wiring - no-op today since join codes never carry a token.
        if (!string.IsNullOrEmpty(launchData.sessionToken))
        {
            var authenticator = GetComponent<GameLiftPlayerAuthenticator>();
            if (authenticator != null)
                authenticator.clientPlayerSessionId = launchData.sessionToken;
        }

        bool portSet = TrySetPortOnTransport(Transport.active, launchData.port);
        if (networkManager != null)
        {
            if (networkManager.transport != null && networkManager.transport != Transport.active)
                portSet |= TrySetPortOnTransport(networkManager.transport, launchData.port);
        }

        Debug.Log($"[SteamLobby] LaunchRequest aplicado. mode={networkMode}, host={remoteHostAddress}, port={launchData.port}, portSet={portSet}");
    }

    private static bool TrySetPortOnTransport(Transport transport, ushort port)
    {
        if (transport == null)
            return false;

        Type transportType = transport.GetType();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        string[] propertyNames = { "Port", "port" };
        foreach (string propertyName in propertyNames)
        {
            PropertyInfo property = transportType.GetProperty(propertyName, flags);
            if (property == null || !property.CanWrite)
                continue;

            if (property.PropertyType == typeof(ushort))
            {
                property.SetValue(transport, port);
                return true;
            }

            if (property.PropertyType == typeof(int))
            {
                property.SetValue(transport, (int)port);
                return true;
            }
        }

        string[] fieldNames = { "Port", "port" };
        foreach (string fieldName in fieldNames)
        {
            FieldInfo field = transportType.GetField(fieldName, flags);
            if (field == null)
                continue;

            if (field.FieldType == typeof(ushort))
            {
                field.SetValue(transport, port);
                return true;
            }

            if (field.FieldType == typeof(int))
            {
                field.SetValue(transport, (int)port);
                return true;
            }
        }

        Debug.LogWarning($"[SteamLobby] No se encontró campo/propiedad de puerto en transporte {transportType.Name}.");
        return false;
    }

    private static bool TryGetPortFromTransport(Transport transport, out ushort port)
    {
        port = 0;
        if (transport == null)
            return false;

        Type transportType = transport.GetType();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        foreach (string name in new[] { "Port", "port" })
        {
            PropertyInfo property = transportType.GetProperty(name, flags);
            if (property != null && property.CanRead && TryConvertToUshort(property.GetValue(transport), out port))
                return true;

            FieldInfo field = transportType.GetField(name, flags);
            if (field != null && TryConvertToUshort(field.GetValue(transport), out port))
                return true;
        }

        return false;
    }

    private static bool TryConvertToUshort(object value, out ushort result)
    {
        result = 0;
        if (value is ushort u) { result = u; return true; }
        if (value is int i && i >= 0 && i <= ushort.MaxValue) { result = (ushort)i; return true; }
        return false;
    }

    // -------------------------------------------------------
    //  TRANSPORTE
    // -------------------------------------------------------
    private void ApplyTransportOverride()
    {
        Transport preferred = overrideTransport;

        // NAT hole punching (HolePunchClient) needs full control of the client's local
        // UDP port, which isn't possible with the vendored kcp2k library (see
        // PersonalizedTransport.preferredLocalPort) - prefer our own transport over
        // KcpTransport when nothing was explicitly configured in the Inspector.
        if (preferred == null)
            preferred = GetComponent<PersonalizedTransport>();

        if (preferred == null) return;

        Transport.active = preferred;
        networkManager.transport = preferred;
        Debug.Log($"[SteamLobby] Transporte activo: {preferred.GetType().Name}");
    }

    // -------------------------------------------------------
    //  STEAM
    // -------------------------------------------------------
    private void StartSteam()
    {
        if (!SteamManager.Initialized)
        {
            Debug.LogError("[SteamLobby] SteamManager no inicializado. ¿Está Steam abierto?");

            // En editor puede ser útil caer a DirectHost/DirectJoin para debug.
            if (Application.isEditor)
            {
                Debug.LogWarning("[SteamLobby] Fallback en Editor: Steam no está inicializado. Se aplicará comportamiento directo para debug.");
                // Imitamos AutoByClone aquí: si es clone hacemos join, si no host.
                if (Application.dataPath.ToLower().Contains("clone"))
                {
                    preventHosting = true;
                    Invoke(nameof(JoinDirect), joinDelay);
                }
                else
                {
                    HostDirect();
                }
            }
            return;
        }

        lobbyCreated           = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
        gameLobbyJoinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequested);
        lobbyEntered           = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
        Debug.Log("[SteamLobby] Callbacks de Steam inicializados.");
    }

    // -------------------------------------------------------
    //  DIRECTO (sin Steam)
    // -------------------------------------------------------
    private void HostDirect()
    {
        if (preventHosting) return;
        Debug.Log($"[SteamLobby] Iniciando Host directo. transport={Transport.active?.GetType().Name ?? "null"}");
        SetStatus("Hosteando. Esperando jugadores...");
        networkManager.StartHost();
        Debug.Log("[SteamLobby] Host iniciado.");

        // Skip in-Editor: local/ParrelSync testing connects via loopback (see
        // UI_OnlineDirectMenu), so none of this is needed, and it'd otherwise touch the
        // developer's own router/firewall on every playtest for no benefit.
        if (!Application.isEditor)
        {
            ushort port = TryGetPortFromTransport(Transport.active, out ushort detectedPort) ? detectedPort : (ushort)7777;

            // Separate problem from router/NAT traversal (UPnP, hole punching below) -
            // Windows can silently block inbound traffic on its own regardless of how
            // it reached the network card, especially on networks marked "Public".
            WindowsFirewallHelper.EnsureInboundUdpRule(port);

            if (autoPortForward)
            {
                lastMappedPort = port;
                StartCoroutine(UpnpPortMapper.TryMapPort(port, "BloomNDoom-GameHost", success =>
                    Debug.Log(success
                        ? $"[SteamLobby] UPnP: puerto {port} abierto automáticamente."
                        : "[SteamLobby] UPnP: no se pudo abrir el puerto automáticamente (router sin UPnP o CGNAT). Puede necesitar forwarding manual.")));
            }

            // Register with the signaling server (if configured) so a joiner who can't
            // reach us directly (UPnP failed, no manual forwarding) can still find us
            // via NAT hole punching. No-ops silently if HolePunchClient isn't configured.
            holePunchClient?.BeginHosting(roomCode);
        }
    }

    private void OnApplicationQuit()
    {
        if (lastMappedPort != 0)
            StartCoroutine(UpnpPortMapper.TryUnmapPort(lastMappedPort));

        holePunchClient?.StopHosting();
    }

    private void JoinDirect()
    {
        Debug.Log($"[SteamLobby] Intentando Join directo a {remoteHostAddress} (transport={Transport.active?.GetType().Name ?? "null"})");
        StartCoroutine(JoinWithFallback());
    }

    // Tries remoteHostAddress first (typically the host's LAN IP - fast, doesn't depend
    // on NAT/UPnP), then fallbackHostAddress (the host's public IP, in case UPnP/manual
    // forwarding worked), and if both fail, asks the signaling server to resolve the
    // room code via NAT hole punching as a last resort. Covers same-network testing and
    // real friends-over-internet play with the same join code either way.
    private IEnumerator JoinWithFallback()
    {
        SetStatus("Conectando (red local)...");
        bool connected = false;
        yield return AttemptConnect(remoteHostAddress, null, success => connected = success);

        if (connected)
        {
            SetStatus("¡Conectado!");
            yield break;
        }

        if (!string.IsNullOrEmpty(fallbackHostAddress) && fallbackHostAddress != remoteHostAddress)
        {
            Debug.LogWarning($"[SteamLobby] No se pudo conectar a {remoteHostAddress}. Reintentando con {fallbackHostAddress}...");
            SetStatus("Probando IP pública...");
            yield return AttemptConnect(fallbackHostAddress, null, success => connected = success);
        }
        else
        {
            Debug.LogWarning($"[SteamLobby] No se pudo conectar a {remoteHostAddress} y no hay dirección de respaldo directa.");
        }

        if (connected)
        {
            SetStatus("¡Conectado!");
            yield break;
        }

        if (holePunchClient == null || !holePunchClient.IsConfigured || string.IsNullOrEmpty(roomCode))
        {
            SetStatus("No se pudo conectar.");
            yield break;
        }

        Debug.LogWarning("[SteamLobby] Intentando conectar vía hole punching (servidor de señalización)...");
        SetStatus("Perforando NAT (hole punching)...");

        IPEndPoint punchedHost = null;
        yield return holePunchClient.TryResolveHost(roomCode, gamePort, ep => punchedHost = ep);

        if (punchedHost == null)
        {
            Debug.LogWarning("[SteamLobby] No se pudo resolver el host vía hole punching.");
            SetStatus("No se pudo conectar.");
            yield break;
        }

        yield return AttemptConnect(punchedHost.Address.ToString(), (ushort)punchedHost.Port, success =>
        {
            if (success)
            {
                SetStatus("¡Conectado!");
            }
            else
            {
                Debug.LogWarning("[SteamLobby] Hole punching tampoco logró conectar.");
                SetStatus("No se pudo conectar.");
            }
        });
    }

    private IEnumerator AttemptConnect(string address, ushort? portOverride, Action<bool> onFinished)
    {
        if (portOverride.HasValue)
            TrySetPortOnTransport(Transport.active, portOverride.Value);

        bool finished = false;
        bool succeeded = false;

        void HandleConnected() { succeeded = true; finished = true; }
        void HandleDisconnected() { finished = true; }

        // NetworkClient.OnConnectedEvent/OnDisconnectedEvent can't be used here -
        // NetworkManager.StartClient() overwrites them with `=` on every call, silently
        // dropping this subscription. OnlineNetworkManager's events wrap Mirror's
        // OnClientConnect/OnClientDisconnect virtual methods instead, which are the
        // actual supported extension point and fire reliably (after authentication).
        if (onlineNetworkManager != null)
        {
            onlineNetworkManager.ClientConnectedOnline += HandleConnected;
            onlineNetworkManager.ClientDisconnectedOnline += HandleDisconnected;
        }

        networkManager.networkAddress = address;
        networkManager.StartClient();
        Debug.Log($"[SteamLobby] Cliente conectando a {address}...");

        float elapsed = 0f;
        while (!finished && elapsed < directConnectTimeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (onlineNetworkManager != null)
        {
            onlineNetworkManager.ClientConnectedOnline -= HandleConnected;
            onlineNetworkManager.ClientDisconnectedOnline -= HandleDisconnected;
        }

        if (succeeded)
        {
            Debug.Log($"[SteamLobby] Conectado a {address}.");
        }
        else if (NetworkClient.active)
        {
            // Timed out (or disconnected before we could tell) - stop cleanly so a
            // retry with a different address can start fresh.
            networkManager.StopClient();
            yield return null; // let Mirror's teardown finish before the next StartClient()
        }

        onFinished?.Invoke(succeeded);
    }

    // -------------------------------------------------------
    //  API PÚBLICA (botones / scripts externos)
    // -------------------------------------------------------

    /// <summary>Inicia host. En modo Steam crea un lobby; en otros modos llama StartHost directo.</summary>
    public void HostLobby()
    {
        if (preventHosting) return;

        if (networkMode == NetworkMode.Steam && SteamManager.Initialized)
        {
            SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, networkManager.maxConnections);
        }
        else
        {
            HostDirect();
        }
    }

    /// <summary>Conecta como cliente a remoteHostAddress (útil desde UI).</summary>
    public void JoinLobby()
    {
        preventHosting = true;
        JoinDirect();
    }

    // -------------------------------------------------------
    //  CALLBACKS DE STEAM
    // -------------------------------------------------------
    private void OnLobbyCreated(LobbyCreated_t callback)
    {
        if (callback.m_eResult != EResult.k_EResultOK)
        {
            Debug.LogError($"[SteamLobby] Error creando lobby: {callback.m_eResult}");
            return;
        }

        Debug.Log("[SteamLobby] Lobby creado correctamente. Iniciando host...");
        networkManager.StartHost();

        // Guardamos el steam id del host para que otros lo utilicen.
        string hostSteamId = SteamUser.GetSteamID().ToString();
        SteamMatchmaking.SetLobbyData(new CSteamID(callback.m_ulSteamIDLobby), HostAddressKey, hostSteamId);

        Debug.Log($"[SteamLobby] Lobby de Steam creado, host iniciado. HostSteamId={hostSteamId}");
    }

    private void OnGameLobbyJoinRequested(GameLobbyJoinRequested_t callback)
    {
        Debug.Log("[SteamLobby] GameLobbyJoinRequested recibido. Intentando unirse al lobby.");
        SteamMatchmaking.JoinLobby(callback.m_steamIDLobby);
    }

    private void OnLobbyEntered(LobbyEnter_t callback)
    {
        Debug.Log("[SteamLobby] OnLobbyEntered callback recibido.");

        if (NetworkServer.active)
        {
            Debug.Log("[SteamLobby] Esta instancia es servidor. Ignorando OnLobbyEntered.");
            return;
        }

        string hostAddress = SteamMatchmaking.GetLobbyData(new CSteamID(callback.m_ulSteamIDLobby), HostAddressKey);
        Debug.Log($"[SteamLobby] HostAddress (raw) desde lobby: '{hostAddress}'");

        if (string.IsNullOrWhiteSpace(hostAddress))
        {
            Debug.LogError("[SteamLobby] HostAddress vacío en lobby. Abortando conexión.");
            return;
        }

        string transportName = Transport.active?.GetType().Name ?? "null";
        Debug.Log($"[SteamLobby] Transporte activo: {transportName}");

        // Si el transporte activo NO es uno basado en Steam, advertimos y no intentamos usar SteamID como IP.
        if (!transportName.ToLower().Contains("steam") && !transportName.ToLower().Contains("fizzy") && !transportName.ToLower().Contains("p2p"))
        {
            Debug.LogWarning("[SteamLobby] Transporte activo no parece ser Steam. El HostAddress podría no ser una IP válida. Intentando conexión directa usando el valor recibido.");
        }

        networkManager.networkAddress = hostAddress;
        networkManager.StartClient();
        Debug.Log($"[SteamLobby] Cliente conectando a {hostAddress} (transport={transportName})");
    }
}