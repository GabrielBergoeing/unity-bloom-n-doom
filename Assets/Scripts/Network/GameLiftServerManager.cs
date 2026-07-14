using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Mirror;

#if UNITY_SERVER
using Aws.GameLift.Server;
using Aws.GameLift.Server.Model;
#endif

public class GameLiftServerManager : MonoBehaviour
{
#if UNITY_SERVER 
    // ^^^ AÑADIDO AQUÍ: Todo este bloque hasta el final es solo para el servidor

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
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        networkManager = FindObjectOfType<NetworkManager>();
        if (networkManager == null)
        {
            Debug.LogError("[GameLift] No se encontró NetworkManager en la escena.");
            return;
        }

        try
        {
            // Parámetros manuales inyectados para pruebas locales con Anywhere
            ServerParameters serverParams = new ServerParameters(
                "wss://us-east-1.api.amazongamelift.com",    // 1. WebSocket URL
                Guid.NewGuid().ToString(),                   // 2. Process ID (Generado aleatoriamente)
                "Cito-WindowsPC",                            // 3. Host ID (Compute Name)
                "fleet-72471bbe-1659-4eb0-a38a-c3075fcf6de1",// 4. Fleet ID
                "179a4e11-10e7-4c29-8b39-ba7d642ebc19"       // 5. Auth Token
            );

            GameLiftServerAPI.InitSDK(serverParams);
            Debug.Log("[GameLift] SDK inicializado con parámetros manuales.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[GameLift] InitSDK falló: {ex}");
            return;
        }

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
            Debug.LogError($"[GameLift] ProcessReady falló: {outcome.Error}");
    }

    void Update()
    {
        lock (queueLock)
        {
            while (mainThreadActions.Count > 0)
            {
                mainThreadActions.Dequeue()?.Invoke();
            }
        }
    }

    // Invocado por GameLift cuando se inicia una nueva GameSession
    private void OnStartGameSession(GameSession gameSession)
    {
        Debug.Log($"[GameLift] OnStartGameSession: ID={gameSession.GameSessionId}");

        // Enviamos la lógica al hilo principal de Unity
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

                // Activar la sesión en GameLift (AHORA SÍ SE EJECUTARÁ)
                var activateOutcome = GameLiftServerAPI.ActivateGameSession();
                if (!activateOutcome.Success)
                    Debug.LogError($"[GameLift] ActivateGameSession falló: {activateOutcome.Error}");
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

        // El cierre también interactúa con Mirror y Unity, debe ir al hilo principal
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

        Debug.LogWarning($"[GameLift] No se encontró campo/propiedad de puerto en transporte {transportType.Name}.");
        return false;
    }

    // ---------------------------
    //  PLAYER SESSION MANAGEMENT
    // ---------------------------

    public bool TryAcceptPlayerSessionForConnection(string playerSessionId, int connectionId)
    {
        if (string.IsNullOrEmpty(playerSessionId))
        {
            Debug.LogWarning("[GameLift] playerSessionId vacío en TryAcceptPlayerSessionForConnection.");
            return false;
        }

        bool acceptSuccess = InvokeGameLiftAcceptPlayerSession(playerSessionId);
        if (!acceptSuccess)
        {
            Debug.LogWarning($"[GameLift] AcceptPlayerSession falló para PlayerSessionId={playerSessionId}");
            return false;
        }

        acceptedPlayerSessions[playerSessionId] = connectionId;
        Debug.Log($"[GameLift] PlayerSession aceptada y mapeada: {playerSessionId} -> connId {connectionId}");

        if (NetworkServer.connections.TryGetValue(connectionId, out NetworkConnectionToClient conn))
        {
            if (networkManager.playerPrefab == null)
            {
                Debug.LogError("[GameLift] playerPrefab no asignado en NetworkManager. No se puede crear player.");
            }
            else
            {
                NetworkServer.AddPlayerForConnection(conn, networkManager.playerPrefab);
                Debug.Log($"[GameLift] Player creado para connectionId {connectionId}.");
            }
        }
        else
        {
            Debug.LogWarning($"[GameLift] ConnectionId {connectionId} no encontrado en NetworkServer.connections.");
        }

        return true;
    }

    public bool TryRemovePlayerSession(string playerSessionId)
    {
        if (string.IsNullOrEmpty(playerSessionId))
        {
            Debug.LogWarning("[GameLift] playerSessionId vacío en TryRemovePlayerSession.");
            return false;
        }

        bool removeSuccess = InvokeGameLiftRemovePlayerSession(playerSessionId);
        if (!removeSuccess)
        {
            Debug.LogWarning($"[GameLift] RemovePlayerSession falló para PlayerSessionId={playerSessionId}");
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
                Debug.Log($"[GameLift] Conexión {connectionId} desconectada por RemovePlayerSession.");
            }
            acceptedPlayerSessions.Remove(playerSessionId);
        }
        else
        {
            Debug.LogWarning($"[GameLift] No había mapping local para playerSessionId {playerSessionId}.");
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
                Debug.LogWarning("[GameLift] DescribePlayerSessions no disponible en esta versión de API.");
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
                    Debug.LogWarning("[GameLift] No se encontró DescribePlayerSessionsRequest en la asamblea de GameLift.");
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
            Debug.LogWarning($"[GameLift] DescribePlayerSessions falló: {ex}");
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
                Debug.LogWarning("[GameLift] AcceptPlayerSession no disponible en esta versión de API (reflection).");
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
            Debug.LogWarning($"[GameLift] InvokeGameLiftAcceptPlayerSession excepción: {ex}");
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
                Debug.LogWarning("[GameLift] RemovePlayerSession no disponible en esta versión de API (reflection).");
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
            Debug.LogWarning($"[GameLift] InvokeGameLiftRemovePlayerSession excepción: {ex}");
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