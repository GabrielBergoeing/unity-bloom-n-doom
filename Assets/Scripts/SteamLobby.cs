using UnityEngine;
using Mirror;
using Steamworks;

public class SteamLobby : MonoBehaviour
{

    public GameObject hostButton = null;
    private NetworkManager networkManager;

    protected Callback<LobbyCreated_t> lobbyCreated;
    protected Callback<GameLobbyJoinRequested_t> gameLobbyJoinRequested;
    protected Callback<LobbyEnter_t> lobbyEntered;

    private const string HostAddressKey = "HostAddress";
    

    void Start()
    {
        networkManager = GetComponent<NetworkManager>();

        if (!SteamManager.Initialized)
        {
            Debug.LogError("SteamManager is not initialized.");
            return;
        }
        lobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
        gameLobbyJoinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequested);
        lobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
    }

    public void HostLobby()
    {
        hostButton.SetActive(false);
        SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, networkManager.maxConnections);
    }

    private void OnLobbyCreated(LobbyCreated_t callback)
    {
        // 1. Check if it failed first (Early Exit)
        if (callback.m_eResult != EResult.k_EResultOK)
        {
            hostButton.SetActive(true);
            Debug.LogError("Lobby creation failed.");
            return; 
        }

        // 2. If we reach here, it was successful! 
        Debug.Log("Lobby created successfully: " + callback.m_ulSteamIDLobby);

        // 3. START THE HOST (Important: do this before setting data)
        networkManager.StartHost();

        // 4. SET THE DATA so others can find you
        SteamMatchmaking.SetLobbyData(
            new CSteamID(callback.m_ulSteamIDLobby), 
            HostAddressKey, 
            SteamUser.GetSteamID().ToString()
        );
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

        hostButton.SetActive(false);
    }
}

