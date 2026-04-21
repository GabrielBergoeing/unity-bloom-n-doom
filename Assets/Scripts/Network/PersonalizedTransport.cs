using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using UnityEngine;
using Mirror;

public class PersonalizedTransport : Transport
{
    public ushort port = 7777;

    // --- VARIABLES DEL CLIENTE ---
    private UdpClient client; 
    private IPEndPoint serverEndPoint; 

    // --- VARIABLES DEL SERVIDOR ---
    private UdpClient server; 
    
    private Dictionary<IPEndPoint, int> connectedClients = new Dictionary<IPEndPoint, int>();
    private Dictionary<int, float> lastSeenTime = new Dictionary<int, float>();


    [SerializeField]
    private float timeoutDuration = 5f; // Tiempo en segundos para considerar a un cliente desconectado por inactividad

    private int nextConnectionId = 1; 

    private bool connectionPending = false;

    // Indica a Mirror que este transporte está disponible para usarse
    public override bool Available() => true;

    // ========================================================================
    // LÓGICA DEL SERVIDOR
    // ========================================================================

    public override bool ServerActive() => server != null;

    public override void ServerStart() 
    {
        server = new UdpClient(port);
        Debug.Log($"[Servidor] Escuchando en el puerto {port} mediante UDP crudo.");
    }

    public override void ServerSend(int connectionId, ArraySegment<byte> segment, int channelId) 
    {
        foreach (var kvp in connectedClients)
        {
            if (kvp.Value == connectionId)
            {
                byte[] data = segment.ToArray();
                server.Send(data, data.Length, kvp.Key);
                break;
            }
        }
    }

    public override void ServerDisconnect(int connectionId) 
    {
        // En UDP crudo no hay una "desconexión" formal que enviar por defecto,
        // pero debemos limpiar nuestro diccionario de clientes.
        IPEndPoint endpointToRemove = null;
        foreach (var kvp in connectedClients)
        {
            if (kvp.Value == connectionId)
            {
                endpointToRemove = kvp.Key;
                break;
            }
        }

        if (endpointToRemove != null)
        {
            connectedClients.Remove(endpointToRemove);
            lastSeenTime.Remove(connectionId);
            OnServerDisconnected?.Invoke(connectionId);
            Debug.Log($"[Servidor] Cliente {connectionId} desconectado.");
        }
    }

    public override string ServerGetClientAddress(int connectionId) 
    {
        foreach (var kvp in connectedClients)
        {
            if (kvp.Value == connectionId)
                return kvp.Key.ToString();
        }
        return string.Empty;
    }

    public override void ServerStop() 
    {
        if (server != null)
        {
            server.Close();
            server = null;
            connectedClients.Clear();
            Debug.Log("[Servidor] Apagado.");
        }
    }

    public override Uri ServerUri()
    {
        UriBuilder builder = new UriBuilder();
        builder.Scheme = "udp";
        builder.Host = Dns.GetHostName();
        builder.Port = port;
        return builder.Uri;
    }

    // ========================================================================
    // LÓGICA DEL CLIENTE
    // ========================================================================

    public override bool ClientConnected() => client != null;

    public override void ClientConnect(string address) 
    {
        // 1. Forzamos localhost a IPv4
        if (address.ToLower() == "localhost") address = "127.0.0.1";
        
        IPAddress ipAddress;
        if (!IPAddress.TryParse(address, out ipAddress))
        {
            IPAddress[] resolved = Dns.GetHostAddresses(address);
            foreach (IPAddress ip in resolved)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork) 
                {
                    ipAddress = ip; break;
                }
            }
        }

        serverEndPoint = new IPEndPoint(ipAddress, port);
        
        // 2. Inicializamos y VINCULAMOS el socket directamente
        client = new UdpClient();
        client.Connect(serverEndPoint); // <--- ESTO ES NUEVO Y CRUCIAL
        
        Debug.Log($"[Cliente] Socket vinculado a {serverEndPoint.Address}:{port}...");
        
        // 3. Avisamos a Mirror

        connectionPending = true; // Esto es ante de hacer el invoke, por temas de tiempos de ejecución

        //OnClientConnected?.Invoke();
        //Debug.Log("[Cliente] Conectado. Aviso enviado a Mirror.");
    }

    
    public override void ClientSend(ArraySegment<byte> segment, int channelId) 
    {
        if (client != null)
        {
            byte[] data = segment.ToArray();
            client.Send(data, data.Length); 
        }
    }

    public override void ClientDisconnect() 
    {
        if (client != null)
        {
            client.Close();
            client = null;
            OnClientDisconnected?.Invoke();
            Debug.Log("[Cliente] Desconectado.");
        }
    }

    // ========================================================================
    // COMÚN / CICLO DE VIDA
    // ========================================================================

    public override void Shutdown()
    {
        Debug.Log("[Transport] Apagando todo...");
        ClientDisconnect();
        ServerStop();
    }

    public override int GetMaxPacketSize(int channelId = Channels.Reliable)
    {
        // El límite seguro de internet para un paquete UDP (MTU) antes de fragmentarse
        return 1200; 
    }

    // ========================================================================
    // POLLING (LEER DATOS DE LA RED) - ¡AQUÍ ESTÁ LA MAGIA!
    // ========================================================================

    public override void ClientEarlyUpdate() 
    {
        // Si el cliente no está conectado, no hacemos nada
        if (client == null) return;


        if (connectionPending) //Conexiones pendientes
        {
            connectionPending = false;
            OnClientConnected?.Invoke();
            Debug.Log("[Cliente] Conexión notificada a Mirror en un nuevo frame.");
        }
        try
        {
            // Mientras haya mensajes esperando en el buzón...
            while (client.Available > 0)
            {
                // Preparamos una variable vacía para saber de dónde viene el paquete
                IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
                
                
                // ¡Leemos los bytes crudos de internet!
                byte[] data = client.Receive(ref sender);

                // Convertimos el byte[] al ArraySegment que Mirror entiende
                ArraySegment<byte> segment = new ArraySegment<byte>(data);
                
                // Le pasamos el paquete a Mirror para que mueva a los jugadores, procese balas, etc.
                OnClientDataReceived?.Invoke(segment, Channels.Reliable);
            }
        }
        catch (SocketException ex)
        {
            Debug.LogWarning($"[Cliente] Error de lectura (Socket): {ex.Message}");
        }
    }

    public override void ServerEarlyUpdate() 
    {
        // Si el servidor no está encendido, no hacemos nada
        if (server == null) return;

        List<int> toDisconnect = new List<int>();

        foreach (var clientTime in lastSeenTime)
        {
            if (Time.time - clientTime.Value > timeoutDuration)
            {
                toDisconnect.Add(clientTime.Key);
            }
        }

        // Ejecutamos la desconexión para los que se pasaron del tiempo
        foreach (int id in toDisconnect)
        {
            Debug.Log($"[Servidor] Cliente {id} excedió el tiempo de espera. Desconectando...");
            ServerDisconnect(id); 
        }

        try
        {
            while (server.Available > 0)
            {
                IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
                byte[] data = server.Receive(ref sender);

                // ¿Es un cliente nuevo?
                if (!connectedClients.ContainsKey(sender))
                {
                    int newId = nextConnectionId++;
                    connectedClients.Add(sender, newId);
                    
                    Debug.Log($"[Servidor] Nuevo cliente: {sender}. ID: {newId}");
                    OnServerConnectedWithAddress?.Invoke(newId, sender.Address.ToString());
                }

                // Ahora sí tenemos el ID seguro
                int connectionId = connectedClients[sender];
                
                // ¡MAGIA! Actualizamos el último momento en que lo vimos
                lastSeenTime[connectionId] = Time.time;

                // Entregar datos a Mirror
                ArraySegment<byte> segment = new ArraySegment<byte>(data);
                OnServerDataReceived?.Invoke(connectionId, segment, Channels.Reliable);
            }
        }
        catch (SocketException ex)
        {

            if (ex.NativeErrorCode == 10054)
            {
                // No imprimimos Warning para no spamear, el sistema de Timeout
                return; 
            }
            Debug.LogWarning($"[Servidor] Error de lectura: {ex.Message}");
        }
    }
}