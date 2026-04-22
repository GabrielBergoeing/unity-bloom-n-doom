using UnityEngine;
using Mirror;
using Steamworks;

public class SteamLobby : MonoBehaviour
{
    [Header("UI")]
    //public GameObject hostButton = null;

    [Header("Editor Online Test (PersonalizedTransport)")]
    [Tooltip("Enable to bypass KCP/Steam and use PersonalizedTransport for real internet testing in the editor.")]
    [SerializeField] private bool usePersonalizedTransport = false;
    [SerializeField] private Transport personalizedTransport;
    [Tooltip("IP address of the host when running as client in editor. Leave as 127.0.0.1 for localhost.")]
    [SerializeField] private string remoteHostAddress = "127.0.0.1";

    private NetworkManager networkManager;
    private bool isClone = false;

    // Callbacks de Steam
    protected Callback<LobbyCreated_t> lobbyCreated;
    protected Callback<GameLobbyJoinRequested_t> gameLobbyJoinRequested;
    protected Callback<LobbyEnter_t> lobbyEntered;

    private const string HostAddressKey = "HostAddress";

    void Start()
    {
        networkManager = GetComponent<NetworkManager>();

    #if UNITY_EDITOR
            if (usePersonalizedTransport && personalizedTransport != null)
            {
                Transport.active = personalizedTransport;
                networkManager.transport = personalizedTransport;
                Debug.Log("[SteamLobby] Using PersonalizedTransport for editor online test.");
            }

            // En el editor usamos KCP directamente, sin Steam
            if (Application.dataPath.Contains("clone"))
            {
                isClone = true;
                Debug.Log("--- MODO CLON ACTIVO: Conectando a " + remoteHostAddress + " ---");
                //hostButton.SetActive(false);
                Invoke(nameof(ConnectAsLocalClient), 1.5f);
            }
            else
            {
                Debug.Log("--- MODO EDITOR: Hosting ---");
                HostLobby();
                // hostButton sigue activo para iniciar host manualmente
            }
    #else
            // BUILD: Usa Steam
            if (!SteamManager.Initialized)
            {
                Debug.LogError("SteamManager no inicializado. Asegúrate de tener Steam abierto.");
                return;
            }

            InitSteamCallbacks();
    #endif
    }

    private void InitSteamCallbacks()
    {
        lobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
        gameLobbyJoinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequested);
        lobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
        Debug.Log("Callbacks de Steam inicializados correctamente.");
    }

    private void ConnectAsLocalClient()
    {
        string address = usePersonalizedTransport ? remoteHostAddress : "localhost";
        Debug.Log($"Clon intentando conectar a {address}...");
        networkManager.networkAddress = address;
        networkManager.StartClient();
    }

    public void HostLobby()
    {
        if (isClone) return;

        //hostButton.SetActive(false);

    #if UNITY_EDITOR
            // En el editor solo iniciamos host con KCP
            networkManager.StartHost();
            Debug.Log("Host iniciado con KCP (editor).");
    #else
            // BUILD: Creamos lobby en Steam
            SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, networkManager.maxConnections);
    #endif
    }

    // --- CALLBACKS DE STEAM (Solo se ejecutan en la instancia principal) ---

    private void OnLobbyCreated(LobbyCreated_t callback)
    {
        if (callback.m_eResult != EResult.k_EResultOK)
        {
            //hostButton.SetActive(true);
            return; 
        }

        networkManager.StartHost();

        SteamMatchmaking.SetLobbyData(
            new CSteamID(callback.m_ulSteamIDLobby), 
            HostAddressKey, 
            SteamUser.GetSteamID().ToString()
        );
        Debug.Log("Lobby de Steam creado e iniciando Host.");
    }

    private void OnGameLobbyJoinRequested(GameLobbyJoinRequested_t callback)
    {
        SteamMatchmaking.JoinLobby(callback.m_steamIDLobby);
    }

    private void OnLobbyEntered(LobbyEnter_t callback)
    {
        if (NetworkServer.active) return;

        string hostAddress = SteamMatchmaking.GetLobbyData(new CSteamID(callback.m_ulSteamIDLobby), HostAddressKey);
        
        networkManager.networkAddress = hostAddress;
        networkManager.StartClient();
        //hostButton.SetActive(false);
    }
}