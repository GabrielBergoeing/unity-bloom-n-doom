using System;
using System.Collections;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

// Requests a game session from Tools/GameLiftBroker (never talks to AWS directly - the
// broker is the only thing that holds AWS credentials, see its README for why). Returns
// the session's {ip/dnsName, port} plus a PlayerSessionId as ConnectionInfo.sessionToken
// - the same shape JoinCodeConnectionProvider produces. SteamLobby.ApplyRuntimeLaunchRequest
// already forwards sessionToken into GameLiftPlayerAuthenticator.clientPlayerSessionId,
// so nothing else in the connect pipeline needs to change for this to work.
//
// Implements IAsyncConnectionProvider, not IConnectionProvider - requesting a session is
// a network round-trip (broker -> AWS), unlike JoinCodeConnectionProvider's synchronous
// local decode, so it can't honestly satisfy IConnectionProvider's synchronous contract.
public class GameLiftConnectionProvider : IAsyncConnectionProvider
{
    private readonly string brokerUrl;
    private readonly int timeoutSeconds;

    // The broker itself waits up to 30s for a freshly-created GameSession to go ACTIVE
    // (see Tools/GameLiftBroker's SessionActivationTimeout) before it can even reply, so
    // this needs enough headroom over that instead of the ~4s used for the direct-IP
    // path's own quick reachability checks - otherwise a legitimate "still starting up"
    // wait gets misreported as a broken/unreachable broker.
    public GameLiftConnectionProvider(string brokerUrl, int timeoutSeconds = 35)
    {
        this.brokerUrl = brokerUrl.TrimEnd('/');
        this.timeoutSeconds = timeoutSeconds;
    }

    public IEnumerator TryRequestConnectionAsync(string userInput, Action<bool, ConnectionInfo, string> onComplete)
    {
        using UnityWebRequest request = new UnityWebRequest($"{brokerUrl}/request-session", "POST")
        {
            uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes("{}")),
            downloadHandler = new DownloadHandlerBuffer(),
            timeout = timeoutSeconds
        };
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            string reason = request.result == UnityWebRequest.Result.ConnectionError && request.responseCode == 0
                ? "tiempo de espera agotado o servidor inalcanzable"
                : request.error;
            onComplete(false, default, $"No se pudo contactar al broker de GameLift: {reason}");
            yield break;
        }

        BrokerResponse response;
        try
        {
            response = JsonConvert.DeserializeObject<BrokerResponse>(request.downloadHandler.text);
        }
        catch (JsonException ex)
        {
            onComplete(false, default, $"Respuesta inválida del broker: {ex.Message}");
            yield break;
        }

        if (response == null || !response.success)
        {
            onComplete(false, default, response?.error ?? "Respuesta vacía del broker.");
            yield break;
        }

        var info = new ConnectionInfo
        {
            address = response.address,
            fallbackAddress = null, // GameLift ya entrega una dirección concreta y alcanzable
            port = (ushort)response.port,
            sessionToken = response.playerSessionId
        };

        onComplete(true, info, null);
    }

    private class BrokerResponse
    {
        public bool success;
        public string address;
        public int port;
        public string playerSessionId;
        public string error;
    }
}
