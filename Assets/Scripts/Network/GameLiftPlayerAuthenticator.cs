using System;
using UnityEngine;
using Mirror;

public class GameLiftPlayerAuthenticator : NetworkAuthenticator
{
    public struct AuthRequestMessage : NetworkMessage
    {
        public string playerSessionId;
    }

    public struct AuthResponseMessage : NetworkMessage
    {
        public bool success;
        public string reason;
    }

    [Header("Cliente")]
    [Tooltip("PlayerSessionId que el cliente enviará al autenticar. Setear desde UI de pruebas.")]
    public string clientPlayerSessionId;

    [Header("Desarrollo y Pruebas")]
    [Tooltip("Activa esto para aceptar automáticamente las conexiones, ignorando GameLift.")]
    public bool bypassAuthentication = false;

    void Awake()
    {
        // No-op
    }

    public override void OnStartServer()
    {
        NetworkServer.RegisterHandler<AuthRequestMessage>(OnAuthRequestMessage, false);
        Debug.Log("[Authenticator] Server handler registrado.");
    }

    public override void OnStartClient()
    {
        NetworkClient.RegisterHandler<AuthResponseMessage>(OnAuthResponseMessage, false);
        Debug.Log("[Authenticator] Client handler registrado.");
    }

    public override void OnServerAuthenticate(NetworkConnectionToClient conn)
    {
        Debug.Log($"[Authenticator] OnServerAuthenticate: connId={conn.connectionId}. Esperando AuthRequestMessage...");
    }

    public override void OnClientAuthenticate()
    {
        if (string.IsNullOrEmpty(clientPlayerSessionId))
        {
            Debug.LogWarning("[Authenticator] clientPlayerSessionId vacío. Enviando cadena vacía para pruebas locales.");
        }

        var msg = new AuthRequestMessage { playerSessionId = clientPlayerSessionId ?? string.Empty };
        NetworkClient.Send(msg);
        Debug.Log("[Authenticator] AuthRequestMessage enviado desde cliente.");
    }

    // ======================
    //  SERVER: manejar request
    // ======================
    private void OnAuthRequestMessage(NetworkConnectionToClient conn, AuthRequestMessage msg)
    {
        string playerSessionId = msg.playerSessionId ?? string.Empty;
        Debug.Log($"[Authenticator] AuthRequestMessage recibido. connId={conn.connectionId}, playerSessionId='{playerSessionId}'");

        // 1. Revisar si el bypass de pruebas está activo
        if (bypassAuthentication)
        {
            conn.Send(new AuthResponseMessage { success = true, reason = "Bypass de pruebas activado" });
            Debug.Log("[Authenticator] Modo Bypass: Aceptación automática ignorando GameLift.");
            ServerAccept(conn);
            return; // Detenemos la ejecución aquí
        }

        // 2. Lógica normal de validación de GameLift
#if UNITY_SERVER
        if (GameLiftServerManager.Instance != null)
        {
            bool accepted = false;
            try
            {
                accepted = GameLiftServerManager.Instance.TryAcceptPlayerSessionForConnection(playerSessionId, conn.connectionId);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Authenticator] Excepción al aceptar player session: {ex}");
                accepted = false;
            }

            if (accepted)
            {
                conn.Send(new AuthResponseMessage { success = true, reason = string.Empty });
                ServerAccept(conn);
                Debug.Log($"[Authenticator] PlayerSession aceptada por GameLift. connId={conn.connectionId}");
            }
            else
            {
                conn.Send(new AuthResponseMessage { success = false, reason = "PlayerSession inválida" });
                Debug.LogWarning($"[Authenticator] PlayerSession rechazada por GameLift. connId={conn.connectionId}");
                ServerReject(conn);
            }
        }
        else
        {
            conn.Send(new AuthResponseMessage { success = false, reason = "GameLift manager no inicializado" });
            Debug.LogError("[Authenticator] GameLiftServerManager.Instance es null en servidor.");
            ServerReject(conn);
        }
#else
        conn.Send(new AuthResponseMessage { success = true, reason = "Local dev accept" });
        Debug.Log("[Authenticator] Modo local: aceptación automática.");
        ServerAccept(conn);
#endif
    }

    // ======================
    //  CLIENT: manejar respuesta
    // ======================
    private void OnAuthResponseMessage(AuthResponseMessage msg)
    {
        if (msg.success)
        {
            Debug.Log("[Authenticator] AuthResponse: success. Cliente autenticado.");
            ClientAccept();
        }
        else
        {
            Debug.LogWarning($"[Authenticator] AuthResponse: failed. Reason={msg.reason}");
            ClientReject();
        }
    }
}