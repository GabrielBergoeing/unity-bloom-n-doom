using System.Collections.Concurrent;
using System.IO;
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
//
// Also runs a small HTTP side-endpoint (separate port, see StartMetricsHttpServer) that
// accepts NetworkMetrics CSV uploads from both host and joiner - since this process is
// already the one rendezvous point both sides of a P2P match can always reach, it's a
// convenient place to collect each side's CSV in one spot instead of digging them out of
// Application.persistentDataPath on every test machine by hand.
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
        StartMetricsHttpServer();

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

    // ------------------------------------------------------------------
    //  Metrics upload (HTTP, separate port from the UDP signaling protocol)
    // ------------------------------------------------------------------

    private static void StartMetricsHttpServer()
    {
        int metricsPort = int.TryParse(Environment.GetEnvironmentVariable("SIGNALING_METRICS_PORT"), out int envPort)
            ? envPort
            : 9051;

        string metricsDir = Path.Combine(AppContext.BaseDirectory, "metrics");
        Directory.CreateDirectory(metricsDir);

        Thread thread = new(() =>
        {
            using HttpListener listener = new();
            listener.Prefixes.Add($"http://+:{metricsPort}/");

            try
            {
                listener.Start();
            }
            catch (HttpListenerException ex)
            {
                Console.WriteLine($"[SignalingServer] No se pudo iniciar el servidor HTTP de métricas en el puerto {metricsPort}: {ex.Message}");
                Console.WriteLine($"[SignalingServer] Si es el error de permisos de Windows, corré una vez como Administrador: netsh http add urlacl url=http://+:{metricsPort}/ user=Everyone");
                return;
            }

            Console.WriteLine($"[SignalingServer] Recibiendo métricas por HTTP en el puerto {metricsPort} (POST /metrics?room=...&role=...&player=...), guardando en {metricsDir}");

            while (true)
            {
                HttpListenerContext context;
                try
                {
                    context = listener.GetContext();
                }
                catch (HttpListenerException)
                {
                    break; // listener stopped
                }

                ThreadPool.QueueUserWorkItem(_ => HandleMetricsUpload(context, metricsDir));
            }
        })
        { IsBackground = true };
        thread.Start();
    }

    private static void HandleMetricsUpload(HttpListenerContext context, string metricsDir)
    {
        try
        {
            if (context.Request.HttpMethod != "POST" || context.Request.Url?.AbsolutePath != "/metrics")
            {
                context.Response.StatusCode = 404;
                return;
            }

            string room = SanitizeForFileName(context.Request.QueryString["room"] ?? "sin-sala");
            string role = SanitizeForFileName(context.Request.QueryString["role"] ?? "sin-rol");
            string player = SanitizeForFileName(context.Request.QueryString["player"] ?? "sin-jugador");

            string csv;
            using (var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding))
                csv = reader.ReadToEnd();

            string fileName = $"{room}_{role}_{player}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
            string path = Path.Combine(metricsDir, fileName);
            File.WriteAllText(path, csv, Encoding.UTF8);

            Console.WriteLine($"[SignalingServer] Métricas recibidas de {context.Request.RemoteEndPoint} -> {fileName} ({csv.Length} bytes)");

            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/json";
            byte[] okBytes = Encoding.UTF8.GetBytes("{\"success\":true}");
            context.Response.OutputStream.Write(okBytes, 0, okBytes.Length);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SignalingServer] Error guardando métricas: {ex.Message}");
            try { context.Response.StatusCode = 500; } catch { /* response already closed */ }
        }
        finally
        {
            context.Response.Close();
        }
    }

    private static string SanitizeForFileName(string value)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            value = value.Replace(c, '_');
        return value;
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
