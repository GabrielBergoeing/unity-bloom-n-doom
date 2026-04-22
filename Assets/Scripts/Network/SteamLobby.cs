using UnityEngine;
using Mirror;
using Steamworks;

public class SteamLobby : MonoBehaviour
{
    public enum NetworkMode
    {
        Steam,          // Usa FizzySteamworks + lobbies de Steam
        DirectHost,     // Host directo con el transporte elegido (sin Steam)
        DirectJoin,     // Join directo a remoteHostAddress con el transporte elegido
        AutoByClone,    // Host si es la instancia principal, Join si es clone de ParrelSync
        Manual          // No hace nada al Start; HostLobby() se llama desde otro script/botón
    }

    [Header("Modo de red")]
    [Tooltip("Elige cómo se comporta esta escena al entrar. Funciona igual en editor y en build.")]
    [SerializeField] private NetworkMode networkMode = NetworkMode.AutoByClone;

    [Header("Transporte")]
    [Tooltip("Transporte que se activará al iniciar. Si es null, no se toca el transporte activo.")]
    [SerializeField] private Transport overrideTransport;

    [Header("Conexión directa (DirectHost / DirectJoin / AutoByClone)")]
    [Tooltip("IP del host cuando esta instancia actúa como cliente.")]
    [SerializeField] private string remoteHostAddress = "127.0.0.1";
    [Tooltip("Segundos de espera antes de intentar conectar como cliente.")]
    [SerializeField] private float joinDelay = 1.5f;

    // -------------------------------------------------------
    private NetworkManager networkManager;
    private bool preventHosting = false;

    protected Callback<LobbyCreated_t> lobbyCreated;
    protected Callback<GameLobbyJoinRequested_t> gameLobbyJoinRequested;
    protected Callback<LobbyEnter_t> lobbyEntered;

    private const string HostAddressKey = "HostAddress";

    // -------------------------------------------------------
    void Start()
    {
        networkManager = GetComponent<NetworkManager>();

        ApplyTransportOverride();

        switch (networkMode)
        {
            case NetworkMode.Steam:
                StartSteam();
                break;

            case NetworkMode.DirectHost:
                Debug.Log("[SteamLobby] Modo DirectHost.");
                HostDirect();
                break;

            case NetworkMode.DirectJoin:
                preventHosting = true;
                Debug.Log($"[SteamLobby] Modo DirectJoin → {remoteHostAddress}");
                Invoke(nameof(JoinDirect), joinDelay);
                break;

            case NetworkMode.AutoByClone:
                if (Application.dataPath.Contains("clone"))
                {
                    preventHosting = true;
                    Debug.Log($"[SteamLobby] AutoByClone: clone detectado → Join a {remoteHostAddress}");
                    Invoke(nameof(JoinDirect), joinDelay);
                }
                else
                {
                    Debug.Log("[SteamLobby] AutoByClone: instancia principal → Host.");
                    HostDirect();
                }
                break;

            case NetworkMode.Manual:
                Debug.Log("[SteamLobby] Modo Manual: esperando llamada externa a HostLobby().");
                break;
        }
    }

    // -------------------------------------------------------
    //  TRANSPORTE
    // -------------------------------------------------------
    private void ApplyTransportOverride()
    {
        if (overrideTransport == null) return;

        Transport.active = overrideTransport;
        networkManager.transport = overrideTransport;
        Debug.Log($"[SteamLobby] Transporte activo: {overrideTransport.GetType().Name}");
    }

    // -------------------------------------------------------
    //  STEAM
    // -------------------------------------------------------
    private void StartSteam()
    {
        if (!SteamManager.Initialized)
        {
            Debug.LogError("[SteamLobby] SteamManager no inicializado. ¿Está Steam abierto?");
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
        networkManager.StartHost();
        Debug.Log("[SteamLobby] Host iniciado.");
    }

    private void JoinDirect()
    {
        networkManager.networkAddress = remoteHostAddress;
        networkManager.StartClient();
        Debug.Log($"[SteamLobby] Cliente conectando a {remoteHostAddress}...");
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
        if (callback.m_eResult != EResult.k_EResultOK) return;

        networkManager.StartHost();

        SteamMatchmaking.SetLobbyData(
            new CSteamID(callback.m_ulSteamIDLobby),
            HostAddressKey,
            SteamUser.GetSteamID().ToString()
        );
        Debug.Log("[SteamLobby] Lobby de Steam creado, host iniciado.");
    }

    private void OnGameLobbyJoinRequested(GameLobbyJoinRequested_t callback)
    {
        SteamMatchmaking.JoinLobby(callback.m_steamIDLobby);
    }

    private void OnLobbyEntered(LobbyEnter_t callback)
    {
        if (NetworkServer.active) return;

        string hostAddress = SteamMatchmaking.GetLobbyData(
            new CSteamID(callback.m_ulSteamIDLobby), HostAddressKey);

        networkManager.networkAddress = hostAddress;
        networkManager.StartClient();
        Debug.Log($"[SteamLobby] Lobby de Steam entrado, conectando a {hostAddress}.");
    }
}