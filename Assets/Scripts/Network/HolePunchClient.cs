using System;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

// Coordinates UDP hole punching so a directly-hosted (P2P) game can be reached by a
// friend on another network without either side forwarding ports manually - covers the
// common case (full/restricted-cone NAT on both ends) that UPnP alone doesn't reliably
// solve. Talks to a small external signaling server (see Tools/SignalingServer) that
// only relays tiny address-discovery messages, never game traffic.
//
// How it works: both host and joiner ask the signaling server "what's my address as
// seen from the internet", using the SAME local port their real game connection will
// use - so the discovered external mapping actually matches what the game traffic
// needs, not some unrelated ephemeral socket. The server hands each peer the other's
// discovered address. Both sides then send a few packets directly toward each other to
// "punch" a hole in their own NAT before the real Mirror connection attempt, so the
// router already has an outbound-created mapping that lets the peer's inbound traffic
// through.
//
// Gracefully does nothing if signalingServerHost is left empty - the rest of the
// connect flow (LAN, then direct public IP) is unaffected either way.
public class HolePunchClient : MonoBehaviour
{
    [Header("Servidor de señalización (Tools/SignalingServer)")]
    [Tooltip("Dejar vacío para desactivar el hole punching por completo.")]
    [SerializeField] private string signalingServerHost = "";
    [SerializeField] private ushort signalingServerPort = 9050;
    [SerializeField] private float requestTimeoutSeconds = 3f;
    [SerializeField] private float hostKeepAliveInterval = 5f;
    [SerializeField] private int primingPacketCount = 5;
    [SerializeField] private float primingPacketInterval = 0.15f;

    private PersonalizedTransport personalizedTransport;
    private IPEndPoint signalingEndPoint;
    private bool hosting;
    private string activeRoomCode;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(signalingServerHost);

    // Exposed so other scripts on a different GameObject (e.g. NetworkMetrics on the
    // Player prefab) can reach the same signaling server host for their own purposes
    // (metrics upload) without duplicating this config in the Inspector.
    public string SignalingServerHost => signalingServerHost;

    private void Awake()
    {
        personalizedTransport = GetComponent<PersonalizedTransport>();
    }

    private void OnDestroy()
    {
        StopHosting();
    }

    private bool TryResolveSignalingEndPoint(out IPEndPoint endPoint)
    {
        endPoint = null;
        if (!IsConfigured)
            return false;

        try
        {
            IPAddress address = IPAddress.TryParse(signalingServerHost, out IPAddress parsed)
                ? parsed
                : Dns.GetHostAddresses(signalingServerHost)[0];
            endPoint = new IPEndPoint(address, signalingServerPort);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[HolePunchClient] No se pudo resolver el servidor de señalización '{signalingServerHost}': {ex.Message}");
            return false;
        }
    }

    // ------------------------------------------------------------------
    //  HOST
    // ------------------------------------------------------------------

    // Call once, right after networkManager.StartHost() succeeds (so PersonalizedTransport's
    // server socket is already bound). Keeps re-registering the room with the signaling
    // server for as long as hosting continues, and primes a NAT hole toward every
    // joiner the server reports.
    public void BeginHosting(string roomCode)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(roomCode) || personalizedTransport == null)
            return;

        if (!TryResolveSignalingEndPoint(out signalingEndPoint))
            return;

        activeRoomCode = roomCode;
        hosting = true;

        personalizedTransport.signalingServerEndPoint = signalingEndPoint;
        personalizedTransport.OnRawServerPacketFromSignaling += HandleHostSignalingPacket;

        Debug.Log($"[HolePunchClient] Registrando sala '{roomCode}' en el servidor de señalización {signalingEndPoint}...");
        StartCoroutine(HostKeepAliveLoop());
    }

    public void StopHosting()
    {
        hosting = false;
        if (personalizedTransport != null)
            personalizedTransport.OnRawServerPacketFromSignaling -= HandleHostSignalingPacket;
    }

    private IEnumerator HostKeepAliveLoop()
    {
        byte[] registerMessage = Encoding.UTF8.GetBytes($"HOST {activeRoomCode}");

        while (hosting)
        {
            personalizedTransport.ServerSendRaw(registerMessage, signalingEndPoint);
            yield return new WaitForSeconds(hostKeepAliveInterval);
        }
    }

    private void HandleHostSignalingPacket(byte[] data, IPEndPoint sender)
    {
        string message = Encoding.UTF8.GetString(data).Trim();
        string[] parts = message.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return;

        if (parts[0] == "PEER" && parts.Length >= 3
            && IPAddress.TryParse(parts[1], out IPAddress joinerIp)
            && int.TryParse(parts[2], out int joinerPort))
        {
            var joinerEndPoint = new IPEndPoint(joinerIp, joinerPort);
            Debug.Log($"[HolePunchClient] Perforando hacia el cliente {joinerEndPoint}...");
            StartCoroutine(PrimeFromServerSocket(joinerEndPoint));
        }
    }

    private IEnumerator PrimeFromServerSocket(IPEndPoint target)
    {
        byte[] primingPacket = Encoding.UTF8.GetBytes("PUNCH");
        for (int i = 0; i < primingPacketCount; i++)
        {
            personalizedTransport.ServerSendRaw(primingPacket, target);
            yield return new WaitForSeconds(primingPacketInterval);
        }
    }

    // ------------------------------------------------------------------
    //  JOIN
    // ------------------------------------------------------------------

    // Resolves a room code to the host's address via the signaling server, priming a
    // NAT hole on the way, and reserves localPort on PersonalizedTransport so the
    // caller's subsequent StartClient() reuses the exact same socket/port the punch
    // came from. Calls onResolved(null) if hole punching isn't configured or fails.
    public IEnumerator TryResolveHost(string roomCode, ushort localPort, Action<IPEndPoint> onResolved)
    {
        if (!IsConfigured || personalizedTransport == null || string.IsNullOrWhiteSpace(roomCode))
        {
            onResolved(null);
            yield break;
        }

        if (!TryResolveSignalingEndPoint(out IPEndPoint signaling))
        {
            onResolved(null);
            yield break;
        }

        Task<IPEndPoint> task = Task.Run(() => RequestHostEndPoint(signaling, roomCode, localPort));
        while (!task.IsCompleted)
            yield return null;

        IPEndPoint hostEndPoint = task.Result;
        if (hostEndPoint == null)
        {
            Debug.LogWarning($"[HolePunchClient] No se pudo resolver la sala '{roomCode}' vía señalización.");
            onResolved(null);
            yield break;
        }

        Debug.Log($"[HolePunchClient] Sala '{roomCode}' resuelta a {hostEndPoint}. Perforando desde el puerto local {localPort}...");
        yield return PrimeFromFixedPort(localPort, hostEndPoint);

        // PersonalizedTransport.ClientConnect() will rebind this exact port moments
        // later for the real connection, reusing the NAT mapping the priming just opened.
        personalizedTransport.preferredLocalPort = localPort;
        onResolved(hostEndPoint);
    }

    private IEnumerator PrimeFromFixedPort(ushort localPort, IPEndPoint target)
    {
        UdpClient punchSocket;
        try
        {
            punchSocket = new UdpClient(localPort);
        }
        catch (SocketException)
        {
            // Port briefly still held by the signaling request's own socket teardown -
            // skip priming here and let PersonalizedTransport's own handshake retries
            // (every 0.5s once ClientConnect() runs) do the punching instead.
            yield break;
        }

        using (punchSocket)
        {
            byte[] primingPacket = Encoding.UTF8.GetBytes("PUNCH");
            for (int i = 0; i < primingPacketCount; i++)
            {
                try
                {
                    punchSocket.Send(primingPacket, primingPacket.Length, target);
                }
                catch (SocketException)
                {
                    break;
                }
                yield return new WaitForSeconds(primingPacketInterval);
            }
        }
    }

    private IPEndPoint RequestHostEndPoint(IPEndPoint signaling, string roomCode, ushort localPort)
    {
        try
        {
            using UdpClient udp = new(localPort);
            udp.Client.ReceiveTimeout = Mathf.CeilToInt(requestTimeoutSeconds * 1000f);

            byte[] request = Encoding.UTF8.GetBytes($"JOIN {roomCode}");
            udp.Send(request, request.Length, signaling);

            var remote = new IPEndPoint(IPAddress.Any, 0);
            byte[] response = udp.Receive(ref remote);
            string message = Encoding.UTF8.GetString(response).Trim();
            string[] parts = message.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length >= 3 && parts[0] == "PEER"
                && IPAddress.TryParse(parts[1], out IPAddress ip) && int.TryParse(parts[2], out int port))
                return new IPEndPoint(ip, port);

            return null;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[HolePunchClient] Error consultando al servidor de señalización: {ex.Message}");
            return null;
        }
    }
}
