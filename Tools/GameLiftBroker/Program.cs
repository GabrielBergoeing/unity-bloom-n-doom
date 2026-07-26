using System.Net;
using System.Text.Json;
using System.Threading;
using Amazon;
using Amazon.GameLift;
using Amazon.GameLift.Model;

// Minimal HTTP broker between the game client and AWS GameLift. Holds AWS credentials
// itself (via the SDK's default credential chain - env vars or ~/.aws/credentials on
// THIS machine only, pointed at a narrow-permission IAM user) so the game client never
// needs to. See Assets/Scripts/Network/GameLiftConnectionProvider.cs for the caller.
//
// Protocol - plain JSON over HTTP, one endpoint:
//   POST /request-session
//     200 OK: {"success":true,"address":"...","port":7777,"playerSessionId":"psess-..."}
//     200 OK: {"success":false,"error":"..."}
public static class Program
{
    private static readonly string FleetId = RequireEnv("GAMELIFT_FLEET_ID");
    private static readonly string Location = RequireEnv("GAMELIFT_LOCATION");
    private static readonly string Region = RequireEnv("GAMELIFT_REGION");
    private static readonly int MaxPlayers = int.TryParse(Environment.GetEnvironmentVariable("GAMELIFT_MAX_PLAYERS"), out int mp) ? mp : 4;
    private static readonly int BrokerPort = int.TryParse(Environment.GetEnvironmentVariable("GAMELIFT_BROKER_PORT"), out int bp) ? bp : 8090;
    private static readonly TimeSpan SessionActivationTimeout = TimeSpan.FromSeconds(30);

    private static readonly AmazonGameLiftClient GameLiftClient = new(RegionEndpoint.GetBySystemName(Region));

    // Guards the find-or-create decision below: two /request-session calls arriving before
    // any GameSession is ACTIVE yet would otherwise both see "none found" and each create
    // their own GameSession. Serializing just that check-then-act section (not the whole
    // request) is enough - CreatePlayerSession itself is safe to run concurrently once a
    // session already exists.
    private static readonly SemaphoreSlim SessionLock = new(1, 1);

    public static async Task Main()
    {
        using HttpListener listener = new();
        listener.Prefixes.Add($"http://+:{BrokerPort}/");
        listener.Start();
        Console.WriteLine($"[GameLiftBroker] Escuchando en el puerto {BrokerPort} (fleet={FleetId}, location={Location}, region={Region})...");

        while (true)
        {
            HttpListenerContext context = await listener.GetContextAsync();
            _ = HandleRequestAsync(context); // fire-and-forget: don't block accepting the next connection
        }
    }

    private static async Task HandleRequestAsync(HttpListenerContext context)
    {
        try
        {
            if (context.Request.HttpMethod == "POST" && context.Request.Url?.AbsolutePath == "/request-session")
            {
                object response = await RequestSessionAsync();
                await WriteJsonAsync(context.Response, response);
            }
            else
            {
                context.Response.StatusCode = 404;
                await WriteJsonAsync(context.Response, new { success = false, error = "Not found" });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GameLiftBroker] Error manejando request: {ex}");
            try
            {
                context.Response.StatusCode = 500;
                await WriteJsonAsync(context.Response, new { success = false, error = ex.Message });
            }
            catch
            {
                // response stream already broken/closed - nothing more to do
            }
        }
    }

    private static async Task<object> RequestSessionAsync()
    {
        GameSession? session;
        await SessionLock.WaitAsync();
        try
        {
            session = await FindActiveSessionAsync();

            if (session == null)
            {
                Console.WriteLine("[GameLiftBroker] No hay sesión activa, creando una nueva...");
                session = await CreateAndWaitForActiveSessionAsync();

                if (session == null)
                {
                    return new
                    {
                        success = false,
                        error = "Timeout esperando que el servidor dedicado active la sesión. ¿Está corriendo el proceso en el compute?"
                    };
                }
            }
        }
        finally
        {
            SessionLock.Release();
        }

        CreatePlayerSessionResponse playerSessionResponse;
        try
        {
            playerSessionResponse = await GameLiftClient.CreatePlayerSessionAsync(new CreatePlayerSessionRequest
            {
                GameSessionId = session.GameSessionId,
                PlayerId = "player-" + Guid.NewGuid()
            });
        }
        catch (GameSessionFullException)
        {
            // Every seat the server process created is spoken for. This normally means the
            // session is genuinely mid-match with a full room, but can also happen if a
            // player's slot never got freed after they left - GameLiftServerManager.
            // OnConnectionDisconnected (server-side) is what's supposed to prevent that.
            return new { success = false, error = "La sala de GameLift está llena. Probá de nuevo en unos segundos." };
        }

        PlayerSession playerSession = playerSessionResponse.PlayerSession;
        string address = !string.IsNullOrEmpty(playerSession.DnsName) ? playerSession.DnsName : playerSession.IpAddress;

        Console.WriteLine($"[GameLiftBroker] Sesión asignada: {address}:{playerSession.Port} (playerSessionId={playerSession.PlayerSessionId})");

        return new
        {
            success = true,
            address,
            port = playerSession.Port,
            playerSessionId = playerSession.PlayerSessionId
        };
    }

    private static async Task<GameSession?> FindActiveSessionAsync()
    {
        DescribeGameSessionsResponse response = await GameLiftClient.DescribeGameSessionsAsync(new DescribeGameSessionsRequest
        {
            FleetId = FleetId,
            StatusFilter = "ACTIVE"
        });

        return response.GameSessions.Count > 0 ? response.GameSessions[0] : null;
    }

    private static async Task<GameSession?> CreateAndWaitForActiveSessionAsync()
    {
        CreateGameSessionResponse createResponse = await GameLiftClient.CreateGameSessionAsync(new CreateGameSessionRequest
        {
            FleetId = FleetId,
            Location = Location,
            MaximumPlayerSessionCount = MaxPlayers
        });

        string gameSessionId = createResponse.GameSession.GameSessionId;
        DateTime deadline = DateTime.UtcNow + SessionActivationTimeout;

        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(1000);

            DescribeGameSessionsResponse poll = await GameLiftClient.DescribeGameSessionsAsync(new DescribeGameSessionsRequest
            {
                GameSessionId = gameSessionId
            });

            GameSession? session = poll.GameSessions.Count > 0 ? poll.GameSessions[0] : null;
            if (session != null && session.Status == GameSessionStatus.ACTIVE)
                return session;
        }

        return null;
    }

    private static async Task WriteJsonAsync(HttpListenerResponse response, object payload)
    {
        response.ContentType = "application/json";
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(payload);
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes);
        response.OutputStream.Close();
    }

    private static string RequireEnv(string name)
    {
        string? value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"Falta la variable de entorno {name}.");
        return value;
    }
}
