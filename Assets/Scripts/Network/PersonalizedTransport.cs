using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using UnityEngine;
using Mirror;

public class PersonalizedTransport : Transport
{
    public ushort port = 7777;

    private static readonly byte[] ConnectHelloPacket = { (byte)'B', (byte)'N', (byte)'D', 1 };
    private static readonly byte[] ConnectAckPacket = { (byte)'B', (byte)'N', (byte)'D', 2 };

    // Every packet after the handshake is tagged with one of these so the receiver
    // knows whether to apply the ack/retry/ordering logic below.
    private enum PacketKind : byte
    {
        Unreliable = 0,
        Reliable = 1,
        Ack = 2
    }

    private const int UnreliableHeaderSize = 1; // [kind]
    private const int ReliableHeaderSize = 5;    // [kind][4-byte seq]
    private const int AckPacketSize = 5;         // [kind][4-byte seq]

    [Header("Reliable channel (ack/retry)")]
    [SerializeField] private float reliableResendInterval = 0.3f;
    [SerializeField] private int reliableMaxResends = 20;

    // Tracks one direction's ack/seq/reorder bookkeeping. Used for the client's link to
    // the server, and (one instance per remote connection) for the server's link to each client.
    private class ReliableChannelState
    {
        public uint nextSendSeq = 1;
        public uint expectedRecvSeq = 1;
        public readonly Dictionary<uint, PendingReliablePacket> pending = new();
        public readonly Dictionary<uint, byte[]> outOfOrder = new();
    }

    private class PendingReliablePacket
    {
        public byte[] data;
        public float lastSentTime;
        public int resendCount;
    }

    // --- VARIABLES DEL CLIENTE ---
    private UdpClient client;
    private IPEndPoint serverEndPoint;
    private bool clientHandshakeComplete;
    private float nextHandshakeAttemptTime;
    private ReliableChannelState clientReliable = new();

    [SerializeField]
    private float handshakeRetryInterval = 0.5f;

    // Optional: set before calling ClientConnect() to bind the client socket to a
    // specific local port instead of letting the OS pick one. Required for UDP hole
    // punching (see HolePunchClient) - the "punch" packets and the real game traffic
    // need to share the same local port so the NAT mapping opened by the punch is
    // reused by the actual connection, instead of the router assigning a fresh
    // (unpunched) mapping for a different ephemeral port.
    public ushort? preferredLocalPort;

    // --- VARIABLES DEL SERVIDOR ---
    private UdpClient server;

    // Set by HolePunchClient while hosting, so ServerEarlyUpdate can tell packets from
    // our own signaling server (Tools/SignalingServer) apart from real Mirror clients
    // sharing the same listening socket/port. The signaling traffic HAS to share this
    // socket rather than use a separate one - the whole point of hole punching is that
    // the NAT mapping opened by talking to the signaling server from this exact port
    // is the one that lets a joiner's traffic on this same port through.
    public IPEndPoint signalingServerEndPoint;
    public event Action<byte[], IPEndPoint> OnRawServerPacketFromSignaling;

    // Lets HolePunchClient send arbitrary raw datagrams (signaling registration,
    // NAT-priming packets) through this same server socket instead of a separate one.
    public void ServerSendRaw(byte[] data, IPEndPoint target)
    {
        if (server == null || data == null || target == null)
            return;

        server.Send(data, data.Length, target);
    }

    private Dictionary<IPEndPoint, int> connectedClients = new Dictionary<IPEndPoint, int>();
    private Dictionary<int, float> lastSeenTime = new Dictionary<int, float>();
    private Dictionary<int, IPEndPoint> connectionEndPoints = new Dictionary<int, IPEndPoint>();
    private Dictionary<int, ReliableChannelState> serverReliable = new Dictionary<int, ReliableChannelState>();


    [SerializeField]
    private float timeoutDuration = 5f; // Tiempo en segundos para considerar a un cliente desconectado por inactividad

    private int nextConnectionId = 1;

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
        if (!connectionEndPoints.TryGetValue(connectionId, out IPEndPoint endPoint))
            return;

        if (channelId == Channels.Unreliable)
        {
            byte[] packet = BuildUnreliablePacket(segment);
            server.Send(packet, packet.Length, endPoint);
            return;
        }

        ReliableChannelState state = GetOrCreateServerReliable(connectionId);
        SendReliable(state, segment, packet => server.Send(packet, packet.Length, endPoint));
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
            connectionEndPoints.Remove(connectionId);
            serverReliable.Remove(connectionId);
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
            lastSeenTime.Clear();
            connectionEndPoints.Clear();
            serverReliable.Clear();
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

    public override bool ClientConnected() => client != null && clientHandshakeComplete;

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
        client = preferredLocalPort.HasValue ? new UdpClient(preferredLocalPort.Value) : new UdpClient();
        client.Connect(serverEndPoint); // <--- ESTO ES NUEVO Y CRUCIAL
        clientHandshakeComplete = false;
        nextHandshakeAttemptTime = Time.unscaledTime;
        clientReliable = new ReliableChannelState(); // fresh seq/ack state per connection attempt

        Debug.Log($"[Cliente] Socket vinculado a {serverEndPoint.Address}:{port}...");
    }


    public override void ClientSend(ArraySegment<byte> segment, int channelId)
    {
        if (client == null) return;

        if (channelId == Channels.Unreliable)
        {
            byte[] packet = BuildUnreliablePacket(segment);
            client.Send(packet, packet.Length);
            return;
        }

        SendReliable(clientReliable, segment, packet => client.Send(packet, packet.Length));
    }

    public override void ClientDisconnect()
    {
        if (client != null)
        {
            client.Close();
            client = null;
            clientHandshakeComplete = false;
            clientReliable = new ReliableChannelState();
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
        // El límite seguro de internet para un paquete UDP (MTU) antes de fragmentarse,
        // menos el header que añadimos nosotros (el más grande de los dos: reliable).
        return 1200 - ReliableHeaderSize;
    }

    // ========================================================================
    // CAPA DE FIABILIDAD (ack + reintento + orden) PARA EL CANAL RELIABLE
    // ========================================================================

    private ReliableChannelState GetOrCreateServerReliable(int connectionId)
    {
        if (!serverReliable.TryGetValue(connectionId, out ReliableChannelState state))
        {
            state = new ReliableChannelState();
            serverReliable[connectionId] = state;
        }
        return state;
    }

    private static void SendReliable(ReliableChannelState state, ArraySegment<byte> segment, Action<byte[]> rawSend)
    {
        uint seq = state.nextSendSeq++;
        byte[] packet = BuildReliablePacket(seq, segment);

        state.pending[seq] = new PendingReliablePacket
        {
            data = packet,
            lastSentTime = Time.unscaledTime,
            resendCount = 0
        };

        rawSend(packet);
    }

    private void ProcessResends(ReliableChannelState state, Action<byte[]> rawSend)
    {
        if (state.pending.Count == 0) return;

        float now = Time.unscaledTime;
        List<uint> toDrop = null;

        foreach (var kvp in state.pending)
        {
            PendingReliablePacket pending = kvp.Value;
            if (now - pending.lastSentTime < reliableResendInterval)
                continue;

            if (pending.resendCount >= reliableMaxResends)
            {
                (toDrop ??= new List<uint>()).Add(kvp.Key);
                continue;
            }

            pending.lastSentTime = now;
            pending.resendCount++;
            rawSend(pending.data);
        }

        if (toDrop != null)
        {
            foreach (uint seq in toDrop)
            {
                state.pending.Remove(seq);
                Debug.LogWarning($"[Transport] Reliable packet seq={seq} excedió los reintentos máximos; se descarta.");
            }
        }
    }

    // Despacha un paquete ya recibido (post-handshake) según su tag de tipo.
    private static void HandleIncomingPacket(
        byte[] data,
        ReliableChannelState state,
        Action<ArraySegment<byte>> onUnreliable,
        Action<ArraySegment<byte>> onReliableInOrder,
        Action<uint> sendAck)
    {
        if (data == null || data.Length < 1) return;

        switch ((PacketKind)data[0])
        {
            case PacketKind.Unreliable:
                onUnreliable(new ArraySegment<byte>(data, UnreliableHeaderSize, data.Length - UnreliableHeaderSize));
                break;

            case PacketKind.Reliable:
            {
                if (data.Length < ReliableHeaderSize) return;
                uint seq = ReadSeq(data);

                // Ack incondicionalmente (incluso duplicados) para que el emisor deje de reintentar.
                sendAck(seq);
                DeliverReliable(state, seq, data, onReliableInOrder);
                break;
            }

            case PacketKind.Ack:
            {
                if (data.Length < AckPacketSize) return;
                state.pending.Remove(ReadSeq(data));
                break;
            }
        }
    }

    private static void DeliverReliable(ReliableChannelState state, uint seq, byte[] data, Action<ArraySegment<byte>> onReliableInOrder)
    {
        if (seq < state.expectedRecvSeq)
            return; // ya entregado antes; era un reintento del emisor.

        if (seq > state.expectedRecvSeq)
        {
            // Llegó fuera de orden: lo guardamos hasta que se llene el hueco.
            if (!state.outOfOrder.ContainsKey(seq))
                state.outOfOrder[seq] = data;
            return;
        }

        onReliableInOrder(new ArraySegment<byte>(data, ReliableHeaderSize, data.Length - ReliableHeaderSize));
        state.expectedRecvSeq++;

        while (state.outOfOrder.TryGetValue(state.expectedRecvSeq, out byte[] buffered))
        {
            state.outOfOrder.Remove(state.expectedRecvSeq);
            onReliableInOrder(new ArraySegment<byte>(buffered, ReliableHeaderSize, buffered.Length - ReliableHeaderSize));
            state.expectedRecvSeq++;
        }
    }

    private static byte[] BuildUnreliablePacket(ArraySegment<byte> segment)
    {
        byte[] packet = new byte[UnreliableHeaderSize + segment.Count];
        packet[0] = (byte)PacketKind.Unreliable;
        Buffer.BlockCopy(segment.Array, segment.Offset, packet, UnreliableHeaderSize, segment.Count);
        return packet;
    }

    private static byte[] BuildReliablePacket(uint seq, ArraySegment<byte> segment)
    {
        byte[] packet = new byte[ReliableHeaderSize + segment.Count];
        packet[0] = (byte)PacketKind.Reliable;
        Buffer.BlockCopy(BitConverter.GetBytes(seq), 0, packet, 1, 4);
        Buffer.BlockCopy(segment.Array, segment.Offset, packet, ReliableHeaderSize, segment.Count);
        return packet;
    }

    private static byte[] BuildAckPacket(uint seq)
    {
        byte[] packet = new byte[AckPacketSize];
        packet[0] = (byte)PacketKind.Ack;
        Buffer.BlockCopy(BitConverter.GetBytes(seq), 0, packet, 1, 4);
        return packet;
    }

    private static uint ReadSeq(byte[] data) => BitConverter.ToUInt32(data, 1);

    // ========================================================================
    // POLLING (LEER DATOS DE LA RED) - ¡AQUÍ ESTÁ LA MAGIA!
    // ========================================================================

    public override void ClientEarlyUpdate()
    {
        // Si el cliente no está conectado, no hacemos nada
        if (client == null) return;

        if (!clientHandshakeComplete && Time.unscaledTime >= nextHandshakeAttemptTime)
        {
            SendRaw(client, ConnectHelloPacket);
            nextHandshakeAttemptTime = Time.unscaledTime + handshakeRetryInterval;
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

                if (IsControlPacket(data, ConnectAckPacket))
                {
                    if (!clientHandshakeComplete)
                    {
                        clientHandshakeComplete = true;
                        OnClientConnected?.Invoke();
                        Debug.Log("[Cliente] Conexión confirmada por el servidor.");
                    }

                    continue;
                }

                if (!clientHandshakeComplete)
                {
                    Debug.LogWarning("[Cliente] Paquete recibido antes del handshake; ignorando.");
                    continue;
                }

                HandleIncomingPacket(data, clientReliable,
                    onUnreliable: payload => OnClientDataReceived?.Invoke(payload, Channels.Unreliable),
                    onReliableInOrder: payload => OnClientDataReceived?.Invoke(payload, Channels.Reliable),
                    sendAck: seq => SendRaw(client, BuildAckPacket(seq)));
            }
        }
        catch (SocketException ex)
        {
            Debug.LogWarning($"[Cliente] Error de lectura (Socket): {ex.Message}");
        }

        ProcessResends(clientReliable, packet => SendRaw(client, packet));
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

                if (signalingServerEndPoint != null && sender.Equals(signalingServerEndPoint))
                {
                    OnRawServerPacketFromSignaling?.Invoke(data, sender);
                    continue;
                }

                int connectionId;
                bool isNewClient = false;

                // ¿Es un cliente nuevo?
                if (!connectedClients.TryGetValue(sender, out connectionId))
                {
                    connectionId = nextConnectionId++;
                    connectedClients.Add(sender, connectionId);
                    connectionEndPoints[connectionId] = sender;
                    isNewClient = true;

                    Debug.Log($"[Servidor] Nuevo cliente: {sender}. ID: {connectionId}");
                    OnServerConnectedWithAddress?.Invoke(connectionId, sender.Address.ToString());
                }

                // ¡MAGIA! Actualizamos el último momento en que lo vimos
                lastSeenTime[connectionId] = Time.time;

                if (IsControlPacket(data, ConnectHelloPacket))
                {
                    SendRaw(server, ConnectAckPacket, sender);

                    if (!isNewClient)
                    {
                        Debug.Log($"[Servidor] Handshake renovado para cliente {connectionId}.");
                    }

                    continue;
                }

                ReliableChannelState state = GetOrCreateServerReliable(connectionId);
                int capturedConnectionId = connectionId;
                IPEndPoint replyEndPoint = sender;

                HandleIncomingPacket(data, state,
                    onUnreliable: payload => OnServerDataReceived?.Invoke(capturedConnectionId, payload, Channels.Unreliable),
                    onReliableInOrder: payload => OnServerDataReceived?.Invoke(capturedConnectionId, payload, Channels.Reliable),
                    sendAck: seq => SendRaw(server, BuildAckPacket(seq), replyEndPoint));
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

        foreach (var kvp in serverReliable)
        {
            if (connectionEndPoints.TryGetValue(kvp.Key, out IPEndPoint endPoint))
                ProcessResends(kvp.Value, packet => SendRaw(server, packet, endPoint));
        }
    }

    private static bool IsControlPacket(byte[] data, byte[] expected)
    {
        if (data == null || data.Length != expected.Length)
            return false;

        for (int i = 0; i < expected.Length; i++)
        {
            if (data[i] != expected[i])
                return false;
        }

        return true;
    }

    private static void SendRaw(UdpClient socket, byte[] data)
    {
        if (socket == null || data == null)
            return;

        socket.Send(data, data.Length);
    }

    private static void SendRaw(UdpClient socket, byte[] data, IPEndPoint endPoint)
    {
        if (socket == null || data == null)
            return;

        if (endPoint == null)
            socket.Send(data, data.Length);
        else
            socket.Send(data, data.Length, endPoint);
    }
}
