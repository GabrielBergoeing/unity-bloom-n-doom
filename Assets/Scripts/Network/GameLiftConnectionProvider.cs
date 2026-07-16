// Placeholder for a future GameLift-backed IConnectionProvider - not wired into the
// active UI yet. When the client-side GameLift matchmaking/game-session-request flow
// gets built, it belongs here: call into the GameLift plugin backend (see
// Packages/com.amazonaws.gamelift and the server-side half already in place at
// Assets/Scripts/Network/GameLiftServerManager.cs), then return the session's
// {ip/dnsName, port} plus a PlayerSessionId as ConnectionInfo.sessionToken - the same
// shape JoinCodeConnectionProvider produces today. SteamLobby.ApplyRuntimeLaunchRequest
// already forwards sessionToken into GameLiftPlayerAuthenticator.clientPlayerSessionId,
// so once this class is implemented, UI_OnlineDirectMenu only needs to swap which
// IConnectionProvider it uses - no other call site changes.
public class GameLiftConnectionProvider : IConnectionProvider
{
    public bool TryRequestConnection(string userInput, out ConnectionInfo info, out string error)
    {
        info = default;
        error = "GameLift matchmaking no está implementado todavía.";
        return false;
    }
}
