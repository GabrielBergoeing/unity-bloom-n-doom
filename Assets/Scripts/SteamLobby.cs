using UnityEngine;
using Mirror;
using Steamworks;

public class SteamLobby : MonoBehaviour
{
    [Header("UI")]
    //public GameObject hostButton = null;

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
            // En el editor usamos KCP directamente, sin Steam
            if (Application.dataPath.Contains("clone"))
            {
                isClone = true;
                Debug.Log("--- MODO CLON ACTIVO: Conectando a localhost ---");
                //hostButton.SetActive(false);
                Invoke(nameof(ConnectAsLocalClient), 1.5f);
            }
            else
            {
                Debug.Log("--- MODO EDITOR: Hosting con KCP ---");
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
        Debug.Log("Clon intentando conectar a localhost via KCP...");
        networkManager.networkAddress = "localhost";
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