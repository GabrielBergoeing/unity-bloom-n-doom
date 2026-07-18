// What the connect UI needs to actually reach a server, regardless of how it was
// obtained (a pasted join code today, a GameLift game-session request later).
public struct ConnectionInfo
{
    public string address;
    // Tried first if set; falls back to `address` on failure (e.g. address = LAN IP,
    // fallbackAddress = public IP - covers same-network testing and real internet play
    // with the same code). Null/empty when there's only one candidate.
    public string fallbackAddress;
    public ushort port;
    // Null/empty when not applicable (e.g. plain join codes). Populated by providers
    // that need server-side validation, such as a future GameLift-backed provider -
    // see GameLiftPlayerAuthenticator.clientPlayerSessionId.
    public string sessionToken;
}

// Resolves whatever the player typed/selected into a concrete address+port(+token) to
// connect to. UI_OnlineDirectMenu talks to this instead of parsing raw fields itself,
// so swapping the join-code flow for a GameLift matchmaking flow later only means
// swapping the implementation, not touching the UI or the Mirror connect call sites.
public interface IConnectionProvider
{
    bool TryRequestConnection(string userInput, out ConnectionInfo info, out string error);
}

// Sibling of IConnectionProvider for providers whose connection request is inherently
// asynchronous (a network round-trip to a broker/matchmaker), unlike
// JoinCodeConnectionProvider's purely local, synchronous decode. Deliberately a
// separate interface rather than changing IConnectionProvider itself, so the existing
// synchronous contract (and everything already built on it) stays untouched.
public interface IAsyncConnectionProvider
{
    System.Collections.IEnumerator TryRequestConnectionAsync(string userInput, System.Action<bool, ConnectionInfo, string> onComplete);
}
