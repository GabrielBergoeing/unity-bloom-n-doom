using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

// Minimal UDP rendezvous/signaling server for NAT hole punching (see
// Assets/Scripts/Network/HolePunchClient.cs on the Unity side). This process never
// touches game traffic - it only relays tiny text handshake messages so a host and a
// joiner (each behind their own NAT) can learn each other's real external address and
// punch a hole to connect directly, peer-to-peer.
//
// Protocol - one UTF8 text line per UDP datagram:
//   client -> server: "HELLO"
//   server -> client: "HELLOACK <ip> <port>"        (STUN-lite: your observed external address)
//   host   -> server: "HOST <roomCode>"             (register/refresh - send every few seconds while hosting)
//   server -> host:   "HOSTACK <roomCode>"
//   client -> server: "JOIN <roomCode>"
//   server -> client: "PEER <hostIp> <hostPort>"    (on success)
//   server -> host:   "PEER <joinerIp> <joinerPort>" (on success - so the host can punch back)
//   server -> client: "NOTFOUND <roomCode>"          (no such room, or it expired)
public static class Program
{
    private const int Port = 9050;
    private static readonly TimeSpan RoomTimeout = TimeSpan.FromSeconds(30); // host must re-HOST at least this often

    private sealed class Room
    {
        public required IPEndPoint HostEndPoint;
        public DateTime LastSeen;
    }

    private static readonly ConcurrentDictionary<string, Room> Rooms = new();

    public static void Main()
    {
        using UdpClient udp = new(Port);
        Console.WriteLine($"[SignalingServer] Escuchando UDP en el puerto {Port}...");

        StartRoomCleanupThread();

        while (true)
        {
            IPEndPoint sender = new(IPAddress.Any, 0);
            byte[] data;
            try
            {
                data = udp.Receive(ref sender);
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"[SignalingServer] Error de socket: {ex.Message}");
                continue;
            }

            string message = Encoding.UTF8.GetString(data).Trim();
            HandleMessage(udp, sender, message);
        }
    }

    private static void HandleMessage(UdpClient udp, IPEndPoint sender, string message)
    {
        string[] parts = message.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return;

        switch (parts[0])
        {
            case "HELLO":
                Send(udp, sender, $"HELLOACK {sender.Address} {sender.Port}");
                break;

            case "HOST" when parts.Length >= 2:
                {
                    string roomCode = parts[1];
                    Rooms[roomCode] = new Room { HostEndPoint = sender, LastSeen = DateTime.UtcNow };
                    Console.WriteLine($"[SignalingServer] HOST '{roomCode}' <- {sender}");
                    Send(udp, sender, $"HOSTACK {roomCode}");
                    break;
                }

            case "JOIN" when parts.Length >= 2:
                {
                    string roomCode = parts[1];
                    if (Rooms.TryGetValue(roomCode, out Room? room))
                    {
                        Console.WriteLine($"[SignalingServer] JOIN '{roomCode}' <- {sender} -> host {room.HostEndPoint}");
                        // Tell the joiner where the host is.
                        Send(udp, sender, $"PEER {room.HostEndPoint.Address} {room.HostEndPoint.Port}");
                        // Tell the host where the joiner is, so it can punch back.
                        Send(udp, room.HostEndPoint, $"PEER {sender.Address} {sender.Port}");
                    }
                    else
                    {
                        Send(udp, sender, $"NOTFOUND {roomCode}");
                    }
                    break;
                }

            default:
                Console.WriteLine($"[SignalingServer] Mensaje no reconocido de {sender}: '{message}'");
                break;
        }
    }

    private static void Send(UdpClient udp, IPEndPoint target, string message)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(message);
        udp.Send(bytes, bytes.Length, target);
    }

    private static void StartRoomCleanupThread()
    {
        Thread thread = new(() =>
        {
            while (true)
            {
                Thread.Sleep(5000);
                DateTime cutoff = DateTime.UtcNow - RoomTimeout;
                foreach (KeyValuePair<string, Room> kvp in Rooms)
                {
                    if (kvp.Value.LastSeen < cutoff)
                        Rooms.TryRemove(kvp.Key, out _);
                }
            }
        })
        { IsBackground = true };
        thread.Start();
    }
}
