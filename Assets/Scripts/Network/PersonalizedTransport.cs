using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Hand-rolled raw-UDP transport, ported from the Mirror version to NGO's
/// poll-based NetworkTransport API. Keeps the original hello/ack handshake and
/// the ack + retry + in-order reliable channel, and adds what NGO additionally
/// needs from a transport:
///  - fragmentation for reliable payloads bigger than one datagram
///    (NGO scene sync / spawn batches can be several KB),
///  - sequenced-unreliable delivery (stale packets are dropped, which
///    NetworkTransform state updates rely on),
///  - client keepalives so idle connections don't hit the server's
///    inactivity timeout.
/// Both peers must use this same transport (pick it on both sides in the overlay).
/// </summary>
public class PersonalizedTransport : NetworkTransport
{
    [Header("Connection")]
    public string address = "127.0.0.1";
    public ushort port = 7777;

    private static readonly byte[] ConnectHelloPacket = { (byte)'B', (byte)'N', (byte)'D', 1 };
    private static readonly byte[] ConnectAckPacket = { (byte)'B', (byte)'N', (byte)'D', 2 };

    // Every packet after the handshake is tagged with one of these so the receiver
    // knows whether to apply the ack/retry/ordering logic below.
    private enum PacketKind : byte
    {
        Unreliable = 0,
        Reliable = 1,
        Ack = 2,
        UnreliableSequenced = 3
    }

    private const int UnreliableHeaderSize = 1;    // [kind]
    private const int UnreliableSeqHeaderSize = 5; // [kind][4-byte seq]
    private const int ReliableHeaderSize = 6;      // [kind][4-byte seq][more-fragments flag]
    private const int AckPacketSize = 5;           // [kind][4-byte seq]

    // Safe internet MTU minus our biggest header: reliable payloads larger than
    // this are split into multiple in-order reliable packets and reassembled.
    private const int MaxChunkSize = 1150;

    [Header("Reliable channel (ack/retry)")]
    [SerializeField] private float reliableResendInterval = 0.3f;
    [SerializeField] private int reliableMaxResends = 20;

    [Header("Handshake / keepalive / timeouts")]
    [SerializeField] private float handshakeRetryInterval = 0.5f;
    [SerializeField] private float keepaliveInterval = 2f;      // hello doubles as keepalive
    [SerializeField] private float connectTimeout = 10f;        // give up connecting after this
    [SerializeField] private float timeoutDuration = 5f;        // drop peers not heard from

    // Tracks one direction's ack/seq/reorder/reassembly bookkeeping. One instance for
    // the client's link to the server, and one per remote connection on the server.
    private class ReliableChannelState
    {
        public uint nextSendSeq = 1;
        public uint expectedRecvSeq = 1;
        public readonly Dictionary<uint, PendingReliablePacket> pending = new();
        public readonly Dictionary<uint, byte[]> outOfOrder = new();
        public readonly List<byte> reassembly = new(); // accumulates fragments until "last"

        public uint unreliableSendSeq = 1;
        public uint lastUnreliableRecvSeq = 0;
    }

    private class PendingReliablePacket
    {
        public byte[] data;
        public float lastSentTime;
        public int resendCount;
    }

    // NGO polls for events instead of subscribing to callbacks, so everything the
    // sockets produce is queued here and drained by PollEvent.
    private struct QueuedEvent
    {
        public NetworkEvent type;
        public ulong clientId;
        public byte[] payload;
        public float time;
    }

    private readonly Queue<QueuedEvent> events = new();
    private int lastPumpFrame = -1;

    // --- CLIENT STATE ---
    private UdpClient client;
    private IPEndPoint serverEndPoint;
    private bool clientHandshakeComplete;
    private float nextHelloTime;
    private float connectStartTime;
    private float lastServerPacketTime;
    private ReliableChannelState clientReliable = new();

    // --- SERVER STATE ---
    private UdpClient server;
    private readonly Dictionary<IPEndPoint, ulong> connectedClients = new();
    private readonly Dictionary<ulong, float> lastSeenTime = new();
    private readonly Dictionary<ulong, IPEndPoint> connectionEndPoints = new();
    private readonly Dictionary<ulong, ReliableChannelState> serverReliable = new();
    private ulong nextConnectionId = 1;

    public override ulong ServerClientId => 0;

    public override void Initialize(NetworkManager networkManager = null) { }

    // ========================================================================
    // START / STOP
    // ========================================================================

    public override bool StartServer()
    {
        try
        {
            server = new UdpClient(port);
        }
        catch (SocketException ex)
        {
            Debug.LogError($"[PersonalizedTransport] No se pudo abrir el puerto {port}: {ex.Message}");
            return false;
        }

        Debug.Log($"[PersonalizedTransport] Servidor escuchando en el puerto {port} (UDP crudo).");
        return true;
    }

    public override bool StartClient()
    {
        string target = address != null ? address.Trim() : "";
        if (target.ToLower() == "localhost") target = "127.0.0.1";

        if (!IPAddress.TryParse(target, out IPAddress ipAddress))
        {
            try
            {
                foreach (IPAddress ip in Dns.GetHostAddresses(target))
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork)
                    {
                        ipAddress = ip;
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PersonalizedTransport] No se pudo resolver '{target}': {ex.Message}");
                return false;
            }
        }

        if (ipAddress == null)
        {
            Debug.LogError($"[PersonalizedTransport] Dirección inválida: '{target}'");
            return false;
        }

        serverEndPoint = new IPEndPoint(ipAddress, port);

        client = new UdpClient();
        client.Connect(serverEndPoint);
        clientHandshakeComplete = false;
        connectStartTime = Time.unscaledTime;
        nextHelloTime = Time.unscaledTime;
        lastServerPacketTime = Time.unscaledTime;
        clientReliable = new ReliableChannelState(); // fresh seq/ack state per connection attempt

        Debug.Log($"[PersonalizedTransport] Cliente conectando a {serverEndPoint.Address}:{port}...");
        return true;
    }

    public override void DisconnectRemoteClient(ulong clientId)
    {
        if (!connectionEndPoints.TryGetValue(clientId, out IPEndPoint endPoint))
            return;

        connectedClients.Remove(endPoint);
        connectionEndPoints.Remove(clientId);
        lastSeenTime.Remove(clientId);
        serverReliable.Remove(clientId);
        Debug.Log($"[PersonalizedTransport] Cliente {clientId} desconectado por el servidor.");
    }

    public override void DisconnectLocalClient()
    {
        CloseClient();
    }

    public override void Shutdown()
    {
        CloseClient();

        if (server != null)
        {
            server.Close();
            server = null;
        }
        connectedClients.Clear();
        connectionEndPoints.Clear();
        lastSeenTime.Clear();
        serverReliable.Clear();
        nextConnectionId = 1;
        events.Clear();
    }

    private void CloseClient()
    {
        if (client == null) return;
        client.Close();
        client = null;
        clientHandshakeComplete = false;
        clientReliable = new ReliableChannelState();
    }

    public override ulong GetCurrentRtt(ulong clientId) => 0;

    // ========================================================================
    // SEND
    // ========================================================================

    public override void Send(ulong clientId, ArraySegment<byte> payload, NetworkDelivery delivery)
    {
        // Pick the socket + per-link state for this destination.
        Action<byte[]> rawSend;
        ReliableChannelState state;

        if (server != null && clientId != ServerClientId)
        {
            if (!connectionEndPoints.TryGetValue(clientId, out IPEndPoint endPoint))
                return;
            rawSend = packet => SendRaw(server, packet, endPoint);
            state = GetOrCreateServerReliable(clientId);
        }
        else if (client != null)
        {
            rawSend = packet => SendRaw(client, packet);
            state = clientReliable;
        }
        else
        {
            return;
        }

        switch (delivery)
        {
            case NetworkDelivery.Unreliable:
                rawSend(BuildUnreliablePacket(payload));
                break;

            case NetworkDelivery.UnreliableSequenced:
                rawSend(BuildUnreliableSequencedPacket(state.unreliableSendSeq++, payload));
                break;

            default: // Reliable, ReliableSequenced, ReliableFragmentedSequenced
                SendReliableFragmented(state, payload, rawSend);
                break;
        }
    }

    // Our reliable channel is lossless and in-order, so fragmentation is just
    // "split into chunks, mark all but the last as 'more coming'".
    private void SendReliableFragmented(ReliableChannelState state, ArraySegment<byte> payload, Action<byte[]> rawSend)
    {
        int offset = 0;
        int remaining = payload.Count;

        do
        {
            int chunk = Math.Min(MaxChunkSize, remaining);
            bool more = remaining - chunk > 0;
            var segment = new ArraySegment<byte>(payload.Array, payload.Offset + offset, chunk);

            uint seq = state.nextSendSeq++;
            byte[] packet = BuildReliablePacket(seq, more, segment);

            state.pending[seq] = new PendingReliablePacket
            {
                data = packet,
                lastSentTime = Time.unscaledTime,
                resendCount = 0
            };

            rawSend(packet);

            offset += chunk;
            remaining -= chunk;
        } while (remaining > 0);
    }

    // ========================================================================
    // POLL (NGO drains this every frame)
    // ========================================================================

    public override NetworkEvent PollEvent(out ulong clientId, out ArraySegment<byte> payload, out float receiveTime)
    {
        // Time-based work (resends, keepalives, timeouts) once per frame.
        if (Time.frameCount != lastPumpFrame)
        {
            lastPumpFrame = Time.frameCount;
            PumpClientMaintenance();
            PumpServerMaintenance();
        }

        DrainClientSocket();
        DrainServerSocket();

        if (events.Count > 0)
        {
            QueuedEvent ev = events.Dequeue();
            clientId = ev.clientId;
            payload = ev.payload != null ? new ArraySegment<byte>(ev.payload) : default;
            receiveTime = ev.time;
            return ev.type;
        }

        clientId = 0;
        payload = default;
        receiveTime = Time.realtimeSinceStartup;
        return NetworkEvent.Nothing;
    }

    private void EnqueueEvent(NetworkEvent type, ulong clientId, byte[] payload = null)
    {
        events.Enqueue(new QueuedEvent
        {
            type = type,
            clientId = clientId,
            payload = payload,
            time = Time.realtimeSinceStartup
        });
    }

    // ------------------------------------------------------------------------
    // CLIENT SIDE
    // ------------------------------------------------------------------------

    private void PumpClientMaintenance()
    {
        if (client == null) return;

        float now = Time.unscaledTime;

        // Hello doubles as handshake retry (fast) and keepalive (slow).
        if (now >= nextHelloTime)
        {
            SendRaw(client, ConnectHelloPacket);
            nextHelloTime = now + (clientHandshakeComplete ? keepaliveInterval : handshakeRetryInterval);
        }

        if (!clientHandshakeComplete && now - connectStartTime > connectTimeout)
        {
            Debug.LogWarning("[PersonalizedTransport] Cliente: tiempo de conexión agotado.");
            CloseClient();
            EnqueueEvent(NetworkEvent.Disconnect, ServerClientId);
            return;
        }

        if (clientHandshakeComplete && now - lastServerPacketTime > timeoutDuration)
        {
            Debug.LogWarning("[PersonalizedTransport] Cliente: el servidor dejó de responder.");
            CloseClient();
            EnqueueEvent(NetworkEvent.Disconnect, ServerClientId);
            return;
        }

        ProcessResends(clientReliable, packet => SendRaw(client, packet));
    }

    private void DrainClientSocket()
    {
        if (client == null) return;

        try
        {
            while (client != null && client.Available > 0)
            {
                IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
                byte[] data = client.Receive(ref sender);
                lastServerPacketTime = Time.unscaledTime;
                NetTrafficCounter.AddReceived(data.Length);

                if (IsControlPacket(data, ConnectAckPacket))
                {
                    if (!clientHandshakeComplete)
                    {
                        clientHandshakeComplete = true;
                        nextHelloTime = Time.unscaledTime + keepaliveInterval;
                        EnqueueEvent(NetworkEvent.Connect, ServerClientId);
                        Debug.Log("[PersonalizedTransport] Cliente: conexión confirmada por el servidor.");
                    }
                    continue;
                }

                if (!clientHandshakeComplete)
                    continue; // data before the handshake completes is ignored

                HandleIncomingPacket(data, clientReliable,
                    deliver: bytes => EnqueueEvent(NetworkEvent.Data, ServerClientId, bytes),
                    sendAck: seq => SendRaw(client, BuildAckPacket(seq)));
            }
        }
        catch (SocketException ex)
        {
            Debug.LogWarning($"[PersonalizedTransport] Cliente: error de lectura: {ex.Message}");
        }
    }

    // ------------------------------------------------------------------------
    // SERVER SIDE
    // ------------------------------------------------------------------------

    private void PumpServerMaintenance()
    {
        if (server == null) return;

        List<ulong> toDisconnect = null;
        foreach (var kvp in lastSeenTime)
        {
            if (Time.time - kvp.Value > timeoutDuration)
                (toDisconnect ??= new List<ulong>()).Add(kvp.Key);
        }

        if (toDisconnect != null)
        {
            foreach (ulong id in toDisconnect)
            {
                Debug.Log($"[PersonalizedTransport] Cliente {id} excedió el tiempo de espera. Desconectando...");
                DisconnectRemoteClient(id);
                EnqueueEvent(NetworkEvent.Disconnect, id);
            }
        }

        foreach (var kvp in serverReliable)
        {
            if (connectionEndPoints.TryGetValue(kvp.Key, out IPEndPoint endPoint))
                ProcessResends(kvp.Value, packet => SendRaw(server, packet, endPoint));
        }
    }

    private void DrainServerSocket()
    {
        if (server == null) return;

        try
        {
            while (server != null && server.Available > 0)
            {
                IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
                byte[] data = server.Receive(ref sender);
                NetTrafficCounter.AddReceived(data.Length);

                if (!connectedClients.TryGetValue(sender, out ulong connectionId))
                {
                    connectionId = nextConnectionId++;
                    connectedClients.Add(sender, connectionId);
                    connectionEndPoints[connectionId] = sender;
                    EnqueueEvent(NetworkEvent.Connect, connectionId);
                    Debug.Log($"[PersonalizedTransport] Nuevo cliente: {sender}. ID: {connectionId}");
                }

                lastSeenTime[connectionId] = Time.time;

                if (IsControlPacket(data, ConnectHelloPacket))
                {
                    SendRaw(server, ConnectAckPacket, sender); // handshake + keepalive reply
                    continue;
                }

                ReliableChannelState state = GetOrCreateServerReliable(connectionId);
                ulong capturedConnectionId = connectionId;
                IPEndPoint replyEndPoint = sender;

                HandleIncomingPacket(data, state,
                    deliver: bytes => EnqueueEvent(NetworkEvent.Data, capturedConnectionId, bytes),
                    sendAck: seq => SendRaw(server, BuildAckPacket(seq), replyEndPoint));
            }
        }
        catch (SocketException ex)
        {
            if (ex.NativeErrorCode == 10054)
                return; // remote endpoint reset; the timeout system will clean it up
            Debug.LogWarning($"[PersonalizedTransport] Servidor: error de lectura: {ex.Message}");
        }
    }

    // ========================================================================
    // RELIABILITY LAYER (ack + retry + order + reassembly)
    // ========================================================================

    private ReliableChannelState GetOrCreateServerReliable(ulong connectionId)
    {
        if (!serverReliable.TryGetValue(connectionId, out ReliableChannelState state))
        {
            state = new ReliableChannelState();
            serverReliable[connectionId] = state;
        }
        return state;
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
                Debug.LogWarning($"[PersonalizedTransport] Reliable seq={seq} excedió los reintentos máximos; se descarta.");
            }
        }
    }

    // Dispatches one received datagram (post-handshake) according to its type tag.
    // `deliver` receives a COMPLETE payload (fragments already reassembled).
    private static void HandleIncomingPacket(
        byte[] data,
        ReliableChannelState state,
        Action<byte[]> deliver,
        Action<uint> sendAck)
    {
        if (data == null || data.Length < 1) return;

        switch ((PacketKind)data[0])
        {
            case PacketKind.Unreliable:
                deliver(Slice(data, UnreliableHeaderSize));
                break;

            case PacketKind.UnreliableSequenced:
            {
                if (data.Length < UnreliableSeqHeaderSize) return;
                uint seq = ReadSeq(data);
                if (seq <= state.lastUnreliableRecvSeq)
                    return; // stale — sequenced delivery drops out-of-date packets
                state.lastUnreliableRecvSeq = seq;
                deliver(Slice(data, UnreliableSeqHeaderSize));
                break;
            }

            case PacketKind.Reliable:
            {
                if (data.Length < ReliableHeaderSize) return;
                uint seq = ReadSeq(data);

                // Ack unconditionally (even duplicates) so the sender stops retrying.
                sendAck(seq);
                DeliverReliable(state, seq, data, deliver);
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

    private static void DeliverReliable(ReliableChannelState state, uint seq, byte[] data, Action<byte[]> deliver)
    {
        if (seq < state.expectedRecvSeq)
            return; // already delivered; this was a sender retry.

        if (seq > state.expectedRecvSeq)
        {
            // Out of order: hold it until the gap fills.
            if (!state.outOfOrder.ContainsKey(seq))
                state.outOfOrder[seq] = data;
            return;
        }

        AcceptReliableInOrder(state, data, deliver);
        state.expectedRecvSeq++;

        while (state.outOfOrder.TryGetValue(state.expectedRecvSeq, out byte[] buffered))
        {
            state.outOfOrder.Remove(state.expectedRecvSeq);
            AcceptReliableInOrder(state, buffered, deliver);
            state.expectedRecvSeq++;
        }
    }

    // Appends one in-order reliable packet to the reassembly buffer and delivers
    // the accumulated payload once the "more fragments" flag says it's complete.
    private static void AcceptReliableInOrder(ReliableChannelState state, byte[] data, Action<byte[]> deliver)
    {
        bool more = data[5] != 0;

        for (int i = ReliableHeaderSize; i < data.Length; i++)
            state.reassembly.Add(data[i]);

        if (more) return;

        byte[] complete = state.reassembly.ToArray();
        state.reassembly.Clear();
        deliver(complete);
    }

    // ========================================================================
    // PACKET BUILDING
    // ========================================================================

    private static byte[] BuildUnreliablePacket(ArraySegment<byte> segment)
    {
        byte[] packet = new byte[UnreliableHeaderSize + segment.Count];
        packet[0] = (byte)PacketKind.Unreliable;
        Buffer.BlockCopy(segment.Array, segment.Offset, packet, UnreliableHeaderSize, segment.Count);
        return packet;
    }

    private static byte[] BuildUnreliableSequencedPacket(uint seq, ArraySegment<byte> segment)
    {
        byte[] packet = new byte[UnreliableSeqHeaderSize + segment.Count];
        packet[0] = (byte)PacketKind.UnreliableSequenced;
        Buffer.BlockCopy(BitConverter.GetBytes(seq), 0, packet, 1, 4);
        Buffer.BlockCopy(segment.Array, segment.Offset, packet, UnreliableSeqHeaderSize, segment.Count);
        return packet;
    }

    private static byte[] BuildReliablePacket(uint seq, bool moreFragments, ArraySegment<byte> segment)
    {
        byte[] packet = new byte[ReliableHeaderSize + segment.Count];
        packet[0] = (byte)PacketKind.Reliable;
        Buffer.BlockCopy(BitConverter.GetBytes(seq), 0, packet, 1, 4);
        packet[5] = moreFragments ? (byte)1 : (byte)0;
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

    private static byte[] Slice(byte[] data, int offset)
    {
        byte[] result = new byte[data.Length - offset];
        Buffer.BlockCopy(data, offset, result, 0, result.Length);
        return result;
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
        NetTrafficCounter.AddSent(data.Length); // wire bytes incl. headers/acks/resends
    }

    private static void SendRaw(UdpClient socket, byte[] data, IPEndPoint endPoint)
    {
        if (socket == null || data == null)
            return;

        if (endPoint == null)
            socket.Send(data, data.Length);
        else
            socket.Send(data, data.Length, endPoint);
        NetTrafficCounter.AddSent(data.Length);
    }
}
