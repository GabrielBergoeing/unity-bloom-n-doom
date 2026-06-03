using UnityEngine;
using Mirror;
using Steamworks;
using System;
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
        if (networkManager == null)
        {
            Debug.LogError("[SteamLobby] No se encontró NetworkManager en el mismo GameObject.");
            return;
        }

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
                break;

            default:
                return;
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
        networkManager.StartHost();
        Debug.Log("[SteamLobby] Host iniciado.");
    }

    private void JoinDirect()
    {
        Debug.Log($"[SteamLobby] Intentando Join directo a {remoteHostAddress} (transport={Transport.active?.GetType().Name ?? "null"})");
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