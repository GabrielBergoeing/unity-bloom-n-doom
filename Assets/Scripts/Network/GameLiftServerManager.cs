using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Mirror;

#if UNITY_SERVER
using Aws.GameLift;
using Aws.GameLift.Server;
using Aws.GameLift.Server.Model;
#endif

public class GameLiftServerManager : MonoBehaviour
{
#if UNITY_SERVER 
    // ^^^ A�ADIDO AQU�: Todo este bloque hasta el final es solo para el servidor

    public static GameLiftServerManager Instance { get; private set; }

    private NetworkManager networkManager;

    private readonly object queueLock = new object();
    private readonly Queue<Action> mainThreadActions = new Queue<Action>();

    // Mapea playerSessionId -> connectionId
    private readonly Dictionary<string, int> acceptedPlayerSessions = new Dictionary<string, int>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // Destroy only this duplicate component, not the whole GameObject - a second
            // copy of this component ending up on the same object (e.g. via a stray scene
            // override) must not take down the NetworkManager/first GameLiftServerManager
            // that already succeeded InitSDK()/ProcessReady() on that same object.
            Destroy(this);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        networkManager = FindFirstObjectByType<NetworkManager>();
        if (networkManager == null)
        {
            Debug.LogError("[GameLift] No se encontr� NetworkManager en la escena.");
            return;
        }

        if (!TryInitSdk())
            return;

        int serverPort = GetDefaultTransportPort();

        // Registrar callbacks y marcar proceso listo
        ProcessParameters processParameters = new ProcessParameters(
            OnStartGameSession,
            OnUpdateGameSession, 
            OnProcessTerminate,
            OnHealthCheck,
            serverPort,         
            new LogParameters(new string[] { Application.dataPath + "/server_log.txt" })
        );

        var outcome = GameLiftServerAPI.ProcessReady(processParameters);
        if (outcome.Success)
            Debug.Log("[GameLift] Proceso listo (ProcessReady enviado).");
        else
            Debug.LogError($"[GameLift] ProcessReady fall�: {outcome.Error}");
    }

    private void OnApplicationQuit()
    {
        // Cleans up the SDK's internal websocket connection on a normal quit - GameLift's
        // own OnProcessTerminate->ProcessEnding() handles the case where GameLift itself
        // asks the process to stop, but this covers every other way the process could end.
        GameLiftServerAPI.Destroy();
    }

    // Two deployment models, two InitSDK() overloads - auto-detected rather than toggled by
    // an env var, because a real managed EC2 fleet's RuntimeConfiguration only lets us
    // choose the LaunchPath, not set our own environment variables for the launched
    // process, so there'd be no way to actually set a "use managed mode" flag once deployed:
    //  - Anywhere: a self-registered compute (see Tools/GameLiftLauncher, which fetches a
    //    fresh auth token before every launch and passes everything via env vars under our
    //    own names, since nothing auto-injects them there). Detected by those vars being
    //    present.
    //  - Managed: AWS-managed EC2 fleet. InitSDK() with no parameters - GameLift's own Agent
    //    process on the instance is supposed to inject GAMELIFT_SDK_* env vars before
    //    launching this process, which the SDK reads internally on its own. Falls back to
    //    this whenever the Anywhere vars above aren't found. As of writing this hangs
    //    forever on Linux/AL2023 (confirmed: the same build run locally with those vars set
    //    by hand connects fine, so it's the Agent never setting them - AWS bug, see
    //    https://github.com/amazon-gamelift/amazon-gamelift-plugin-unity/issues/283) -
    //    untested on Windows fleets, where the bug may not reproduce.
    private bool TryInitSdk()
    {
        string websocketUrl = Environment.GetEnvironmentVariable("GAMELIFT_WEBSOCKET_URL");
        string hostId = Environment.GetEnvironmentVariable("GAMELIFT_HOST_ID");
        string fleetId = Environment.GetEnvironmentVariable("GAMELIFT_FLEET_ID");
        string authToken = Environment.GetEnvironmentVariable("GAMELIFT_AUTH_TOKEN");
        bool anywhereConfigured = !string.IsNullOrEmpty(websocketUrl) && !string.IsNullOrEmpty(hostId)
            && !string.IsNullOrEmpty(fleetId) && !string.IsNullOrEmpty(authToken);

        try
        {
            GenericOutcome initOutcome;

            if (anywhereConfigured)
            {
                ServerParameters serverParams = new ServerParameters(
                    websocketUrl,
                    Guid.NewGuid().ToString(), // Process ID - random por cada corrida
                    hostId,
                    fleetId,
                    authToken
                );

                initOutcome = GameLiftServerAPI.InitSDK(serverParams);
            }
            else
            {
                Debug.Log("[GameLift] No se encontraron las variables de Anywhere (GAMELIFT_WEBSOCKET_URL/HOST_ID/FLEET_ID/AUTH_TOKEN) - probando InitSDK() sin parámetros (fleet administrada).");
                initOutcome = GameLiftServerAPI.InitSDK();
            }

            if (!initOutcome.Success)
            {
                Debug.LogError($"[GameLift] InitSDK fall�: {initOutcome.Error}");
                return false;
            }

            Debug.Log($"[GameLift] SDK inicializado (fleet {(anywhereConfigured ? "Anywhere" : "administrada")}).");
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[GameLift] InitSDK fall�: {ex}");
            return false;
        }
    }

    void Update()
    {
        lock (queueLock)
        {
            while (mainThreadActions.Count > 0)
            {
                Action action = mainThreadActions.Dequeue();
                try
                {
                    action?.Invoke();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[GameLift] Excepci�n al ejecutar acci�n encolada desde OnStartGameSession: {ex}");
                }
            }
        }
    }

    // Invocado por GameLift cuando se inicia una nueva GameSession
    private void OnStartGameSession(GameSession gameSession)
    {
        Debug.Log($"[GameLift] OnStartGameSession: ID={gameSession.GameSessionId}");

        // Enviamos la l�gica al hilo principal de Unity
        lock (queueLock)
        {
            mainThreadActions.Enqueue(() =>
            {
                int assignedPort = 0;
                try
                {
                    assignedPort = gameSession.Port;
                }
                catch
                {
                    assignedPort = GetDefaultTransportPort();
                }

                bool portSet = TrySetPortOnTransport(networkManager.transport, (ushort)assignedPort);
                portSet |= TrySetPortOnTransport(Transport.active, (ushort)assignedPort);

                Debug.Log($"[GameLift] Puerto asignado: {assignedPort}, portSet={portSet}");

                // Arrancar servidor Mirror
                StartMirrorServer();

                // Activar la sesi�n en GameLift (AHORA S� SE EJECUTAR�)
                var activateOutcome = GameLiftServerAPI.ActivateGameSession();
                if (!activateOutcome.Success)
                    Debug.LogError($"[GameLift] ActivateGameSession fall�: {activateOutcome.Error}");
                else
                    Debug.Log("[GameLift] GameSession activada y lista.");
            });
        }
    }

    private void OnUpdateGameSession(UpdateGameSession updateGameSession)
    {
        Debug.Log($"[GameLift] OnUpdateGameSession recibido. Motivo: {updateGameSession.UpdateReason}");
    }

    // Health check simple
    private bool OnHealthCheck()
    {
        return true;
    }

    // GameLift invoca cuando pide terminar el proceso
    private void OnProcessTerminate()
    {
        Debug.Log("[GameLift] OnProcessTerminate recibido. Preparando cierre...");

        var outcome = GameLiftServerAPI.ProcessEnding();
        if (!outcome.Success)
            Debug.LogError($"[GameLift] ProcessEnding fallo: {outcome.Error}");

        // El cierre tambi�n interact�a con Mirror y Unity, debe ir al hilo principal
        lock (queueLock)
        {
            mainThreadActions.Enqueue(() =>
            {
                StopMirrorServer();
                Invoke(nameof(ShutdownUnity), 1.0f);
            });
        }
    }

    private void ShutdownUnity()
    {
        Debug.Log("[GameLift] ShutdownUnity: Salida del proceso.");
        Application.Quit();
    }

    private void StartMirrorServer()
    {
        if (networkManager == null)
        {
            Debug.LogError("[GameLift] NetworkManager null, no se puede iniciar servidor Mirror.");
            return;
        }

        if (!NetworkServer.active)
        {
            Debug.Log("[GameLift] Iniciando NetworkManager.StartServer()");
            networkManager.StartServer();
        }
        else
        {
            Debug.LogWarning("[GameLift] NetworkServer ya activo.");
        }
    }

    private void StopMirrorServer()
    {
        if (networkManager == null) return;

        if (NetworkServer.active)
        {
            Debug.Log("[GameLift] Deteniendo servidor Mirror.");
            networkManager.StopServer();
        }
    }

    private int GetDefaultTransportPort()
    {
        ushort port = 7777;
        TrySetPortOnTransport(networkManager.transport, port);
        return port;
    }

    private static bool TrySetPortOnTransport(Transport transport, ushort port)
    {
        if (transport == null) return false;

        Type transportType = transport.GetType();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        string[] propertyNames = { "Port", "port" };
        foreach (string propertyName in propertyNames)
        {
            PropertyInfo property = transportType.GetProperty(propertyName, flags);
            if (property == null || !property.CanWrite) continue;

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
            if (field == null) continue;

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

        Debug.LogWarning($"[GameLift] No se encontr� campo/propiedad de puerto en transporte {transportType.Name}.");
        return false;
    }

    // ---------------------------
    //  PLAYER SESSION MANAGEMENT
    // ---------------------------

    public bool TryAcceptPlayerSessionForConnection(string playerSessionId, int connectionId)
    {
        if (string.IsNullOrEmpty(playerSessionId))
        {
            Debug.LogWarning("[GameLift] playerSessionId vac�o en TryAcceptPlayerSessionForConnection.");
            return false;
        }

        bool acceptSuccess = InvokeGameLiftAcceptPlayerSession(playerSessionId);
        if (!acceptSuccess)
        {
            Debug.LogWarning($"[GameLift] AcceptPlayerSession fall� para PlayerSessionId={playerSessionId}");
            return false;
        }

        acceptedPlayerSessions[playerSessionId] = connectionId;
        Debug.Log($"[GameLift] PlayerSession aceptada y mapeada: {playerSessionId} -> connId {connectionId}");

        // No creamos el player nosotros acá: GameLiftPlayerAuthenticator llama ServerAccept(conn)
        // justo después de que este método devuelve true, lo que marca la conexión como
        // autenticada; con autoCreatePlayer activo en el NetworkManager, el propio cliente pide
        // su player automáticamente (NetworkClient.AddPlayer()), lo que dispara
        // OnlineNetworkManager.OnServerAddPlayer() - el que respeta el personaje elegido. Llamar
        // NetworkServer.AddPlayerForConnection acá con el playerPrefab genérico duplicaba al
        // jugador: uno "fantasma" con el prefab por defecto, más el real vía el flujo normal.

        return true;
    }

    public bool TryRemovePlayerSession(string playerSessionId)
    {
        if (string.IsNullOrEmpty(playerSessionId))
        {
            Debug.LogWarning("[GameLift] playerSessionId vac�o en TryRemovePlayerSession.");
            return false;
        }

        bool removeSuccess = InvokeGameLiftRemovePlayerSession(playerSessionId);
        if (!removeSuccess)
        {
            Debug.LogWarning($"[GameLift] RemovePlayerSession fall� para PlayerSessionId={playerSessionId}");
        }

        if (acceptedPlayerSessions.TryGetValue(playerSessionId, out int connectionId))
        {
            if (NetworkServer.connections.TryGetValue(connectionId, out NetworkConnectionToClient conn))
            {
                if (conn.identity != null)
                {
                    NetworkServer.Destroy(conn.identity.gameObject);
                }
                conn.Disconnect();
                Debug.Log($"[GameLift] Conexi�n {connectionId} desconectada por RemovePlayerSession.");
            }
            acceptedPlayerSessions.Remove(playerSessionId);
        }
        else
        {
            Debug.LogWarning($"[GameLift] No hab�a mapping local para playerSessionId {playerSessionId}.");
        }

        return removeSuccess;
    }

    public string DescribePlayerSessionStatus(string playerSessionId)
    {
        if (string.IsNullOrEmpty(playerSessionId))
            return null;

        try
        {
            Type apiType = typeof(GameLiftServerAPI);
            MethodInfo describeMethod = apiType.GetMethod("DescribePlayerSessions", BindingFlags.Public | BindingFlags.Static);
            if (describeMethod == null)
            {
                Debug.LogWarning("[GameLift] DescribePlayerSessions no disponible en esta versi�n de API.");
                return null;
            }

            object result = null;
            ParameterInfo[] pars = describeMethod.GetParameters();
            if (pars.Length == 1 && pars[0].ParameterType == typeof(string))
            {
                result = describeMethod.Invoke(null, new object[] { playerSessionId });
            }
            else
            {
                Type reqType = null;
                foreach (var t in apiType.Assembly.GetTypes())
                {
                    if (t.Name == "DescribePlayerSessionsRequest")
                    {
                        reqType = t;
                        break;
                    }
                }
                if (reqType != null)
                {
                    object req = Activator.CreateInstance(reqType);
                    PropertyInfo prop = reqType.GetProperty("PlayerSessionId");
                    if (prop != null && prop.CanWrite)
                        prop.SetValue(req, playerSessionId);
                    result = describeMethod.Invoke(null, new object[] { req });
                }
                else
                {
                    Debug.LogWarning("[GameLift] No se encontr� DescribePlayerSessionsRequest en la asamblea de GameLift.");
                    return null;
                }
            }

            if (result != null)
            {
                PropertyInfo sessionsProp = result.GetType().GetProperty("PlayerSessions");
                if (sessionsProp != null)
                {
                    var sessions = sessionsProp.GetValue(result) as System.Collections.IEnumerable;
                    foreach (var s in sessions)
                    {
                        PropertyInfo statusProp = s.GetType().GetProperty("PlayerSessionStatus");
                        if (statusProp != null)
                        {
                            return statusProp.GetValue(s)?.ToString();
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[GameLift] DescribePlayerSessions fall�: {ex}");
        }

        return null;
    }

    // ---------------------------
    //  UTIL: llamadas reflectivas a GameLift
    // ---------------------------

    private bool InvokeGameLiftAcceptPlayerSession(string playerSessionId)
    {
        try
        {
            Type apiType = typeof(GameLiftServerAPI);
            MethodInfo acceptMethod = apiType.GetMethod("AcceptPlayerSession", BindingFlags.Public | BindingFlags.Static);
            if (acceptMethod == null)
            {
                Debug.LogWarning("[GameLift] AcceptPlayerSession no disponible en esta versi�n de API (reflection).");
                return false;
            }

            object result = null;
            ParameterInfo[] pars = acceptMethod.GetParameters();
            if (pars.Length == 1 && pars[0].ParameterType == typeof(string))
            {
                result = acceptMethod.Invoke(null, new object[] { playerSessionId });
            }
            else
            {
                Type reqType = null;
                foreach (var t in apiType.Assembly.GetTypes())
                {
                    if (t.Name == "AcceptPlayerSessionRequest")
                    {
                        reqType = t;
                        break;
                    }
                }
                if (reqType != null)
                {
                    object req = Activator.CreateInstance(reqType);
                    PropertyInfo p = reqType.GetProperty("PlayerSessionId");
                    if (p != null && p.CanWrite) p.SetValue(req, playerSessionId);
                    result = acceptMethod.Invoke(null, new object[] { req });
                }
                else
                {
                    Debug.LogWarning("[GameLift] Tipo AcceptPlayerSessionRequest no encontrado en la asamblea de GameLift.");
                    return false;
                }
            }

            if (result != null)
            {
                PropertyInfo successProp = result.GetType().GetProperty("Success");
                if (successProp != null)
                    return (bool)successProp.GetValue(result);
            }

            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[GameLift] InvokeGameLiftAcceptPlayerSession excepci�n: {ex}");
            return false;
        }
    }

    private bool InvokeGameLiftRemovePlayerSession(string playerSessionId)
    {
        try
        {
            Type apiType = typeof(GameLiftServerAPI);
            MethodInfo removeMethod = apiType.GetMethod("RemovePlayerSession", BindingFlags.Public | BindingFlags.Static);
            if (removeMethod == null)
            {
                Debug.LogWarning("[GameLift] RemovePlayerSession no disponible en esta versi�n de API (reflection).");
                return false;
            }

            object result = null;
            ParameterInfo[] pars = removeMethod.GetParameters();
            if (pars.Length == 1 && pars[0].ParameterType == typeof(string))
            {
                result = removeMethod.Invoke(null, new object[] { playerSessionId });
            }
            else
            {
                Type reqType = null;
                foreach (var t in apiType.Assembly.GetTypes())
                {
                    if (t.Name == "RemovePlayerSessionRequest")
                    {
                        reqType = t;
                        break;
                    }
                }
                if (reqType != null)
                {
                    object req = Activator.CreateInstance(reqType);
                    PropertyInfo p = reqType.GetProperty("PlayerSessionId");
                    if (p != null && p.CanWrite) p.SetValue(req, playerSessionId);
                    result = removeMethod.Invoke(null, new object[] { req });
                }
                else
                {
                    Debug.LogWarning("[GameLift] Tipo RemovePlayerSessionRequest no encontrado en la asamblea de GameLift.");
                    return false;
                }
            }

            if (result != null)
            {
                PropertyInfo successProp = result.GetType().GetProperty("Success");
                if (successProp != null)
                    return (bool)successProp.GetValue(result);
            }

            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[GameLift] InvokeGameLiftRemovePlayerSession excepci�n: {ex}");
            return false;
        }
    }

#else
    void Awake()
    {
        // En cliente/editor no hacemos nada relativo a GameLift
    }
#endif 
}