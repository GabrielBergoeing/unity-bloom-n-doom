using System;
using System.Collections.Generic;
using System.Net;
using kcp2k;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// NGO adapter around the standalone kcp2k library (the same battle-tested KCP
/// implementation Mirror's KcpTransport wraps — kcp2k itself has no Mirror
/// dependency). kcp2k provides its own handshake, reliability, ordering and
/// fragmentation, so this adapter only translates between kcp2k's callbacks
/// and NGO's poll-based NetworkTransport API.
/// </summary>
public class KcpNgoTransport : NetworkTransport
{
    [Header("Connection")]
    public string address = "127.0.0.1";
    public ushort port = 7777;

    [Header("KCP settings")]
    [Tooltip("IPv4 + IPv6 dual socket. Disable if a platform/router misbehaves with IPv6.")]
    [SerializeField] private bool dualMode = true;
    [SerializeField] private int timeoutMs = 10000;

    private KcpServer server;
    private KcpClient client;
    private KcpConfig config;

    // NGO polls for events; kcp2k pushes callbacks — bridge with a queue.
    private struct QueuedEvent
    {
        public NetworkEvent type;
        public ulong clientId;
        public byte[] payload;
        public float time;
    }

    private readonly Queue<QueuedEvent> events = new();
    private int lastTickFrame = -1;

    public override ulong ServerClientId => 0;

    // kcp connection ids are int hashes (can be any value, including 0/negative);
    // shift into 1.. so they can never collide with ServerClientId.
    private static ulong ToNgoId(int kcpId) => (ulong)(uint)kcpId + 1;
    private static int ToKcpId(ulong ngoId) => (int)(uint)(ngoId - 1);

    public override void Initialize(NetworkManager networkManager = null)
    {
        // Route kcp2k's logging into Unity's console.
        Log.Info = Debug.Log;
        Log.Warning = Debug.LogWarning;
        Log.Error = Debug.LogError;

        config = new KcpConfig(
            DualMode: dualMode,
            NoDelay: true,
            Interval: 10,
            FastResend: 2,
            CongestionWindow: false,
            SendWindowSize: 4096,
            ReceiveWindowSize: 4096,
            Timeout: timeoutMs
        );
    }

    // ========================================================================
    // START / STOP
    // ========================================================================

    public override bool StartServer()
    {
        try
        {
            server = new KcpServer(
                OnConnected: (kcpId, endPoint) => EnqueueEvent(NetworkEvent.Connect, ToNgoId(kcpId)),
                OnData: (kcpId, data, channel) =>
                {
                    NetTrafficCounter.AddReceived(data.Count); // NGO payload bytes (kcp wire layer hidden)
                    EnqueueEvent(NetworkEvent.Data, ToNgoId(kcpId), Copy(data));
                },
                OnDisconnected: kcpId => EnqueueEvent(NetworkEvent.Disconnect, ToNgoId(kcpId)),
                OnError: (kcpId, error, reason) =>
                    Debug.LogWarning($"[KcpNgoTransport] Server error for {kcpId}: {error} {reason}"),
                config
            );
            server.Start(port);
            Debug.Log($"[KcpNgoTransport] Servidor KCP escuchando en el puerto {port}.");
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[KcpNgoTransport] No se pudo iniciar el servidor: {ex.Message}");
            server = null;
            return false;
        }
    }

    public override bool StartClient()
    {
        try
        {
            client = new KcpClient(
                OnConnected: () => EnqueueEvent(NetworkEvent.Connect, ServerClientId),
                OnData: (data, channel) =>
                {
                    NetTrafficCounter.AddReceived(data.Count);
                    EnqueueEvent(NetworkEvent.Data, ServerClientId, Copy(data));
                },
                OnDisconnected: () => EnqueueEvent(NetworkEvent.Disconnect, ServerClientId),
                OnError: (error, reason) =>
                    Debug.LogWarning($"[KcpNgoTransport] Client error: {error} {reason}"),
                config
            );
            client.Connect(address, port);
            Debug.Log($"[KcpNgoTransport] Cliente KCP conectando a {address}:{port}...");
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[KcpNgoTransport] No se pudo iniciar el cliente: {ex.Message}");
            client = null;
            return false;
        }
    }

    public override void DisconnectRemoteClient(ulong clientId)
    {
        server?.Disconnect(ToKcpId(clientId));
    }

    public override void DisconnectLocalClient()
    {
        client?.Disconnect();
        client = null;
    }

    public override void Shutdown()
    {
        client?.Disconnect();
        client = null;
        server?.Stop();
        server = null;
        events.Clear();
    }

    public override ulong GetCurrentRtt(ulong clientId) => 0;

    // ========================================================================
    // SEND / POLL
    // ========================================================================

    public override void Send(ulong clientId, ArraySegment<byte> payload, NetworkDelivery delivery)
    {
        // kcp2k's reliable channel is ordered and fragmenting, so every reliable
        // (and sequenced) delivery maps onto it; only plain Unreliable stays raw.
        KcpChannel channel = delivery == NetworkDelivery.Unreliable
            ? KcpChannel.Unreliable
            : KcpChannel.Reliable;

        NetTrafficCounter.AddSent(payload.Count); // NGO payload bytes (kcp wire layer hidden)

        if (server != null && clientId != ServerClientId)
            server.Send(ToKcpId(clientId), payload, channel);
        else
            client?.Send(payload, channel);
    }

    public override NetworkEvent PollEvent(out ulong clientId, out ArraySegment<byte> payload, out float receiveTime)
    {
        // kcp2k needs regular ticks to pump sockets/retransmits — once per frame.
        if (Time.frameCount != lastTickFrame)
        {
            lastTickFrame = Time.frameCount;
            client?.Tick();
            server?.Tick();
        }

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

    // ========================================================================
    // HELPERS
    // ========================================================================

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

    // kcp2k reuses its receive buffers, so payloads must be copied before queueing.
    private static byte[] Copy(ArraySegment<byte> segment)
    {
        byte[] copy = new byte[segment.Count];
        Buffer.BlockCopy(segment.Array, segment.Offset, copy, 0, segment.Count);
        return copy;
    }
}
