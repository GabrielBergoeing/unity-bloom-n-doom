using System;

public static class NetworkLaunchRequest
{
    public enum LaunchMode
    {
        None,
        Host,
        Join
    }

    public struct LaunchData
    {
        public LaunchMode mode;
        public string address;
        // Tried after `address` if the first attempt fails - see SteamLobby.JoinDirect.
        public string fallbackAddress;
        public ushort port;
        // The shared join code, reused as the room id for HolePunchClient's signaling
        // server (host registers it; joiner looks it up as a last-resort fallback after
        // both direct address attempts fail). Null/empty disables hole punching for
        // this session.
        public string roomCode;
        // Set when the connection came from a provider that needs server-side
        // validation (e.g. a future GameLift PlayerSessionId). Null for plain join codes.
        public string sessionToken;
    }

    private static bool hasPendingRequest;
    private static LaunchData pendingData;

    public static void SetHost(ushort port, string roomCode = null)
    {
        pendingData = new LaunchData
        {
            mode = LaunchMode.Host,
            address = "127.0.0.1",
            port = port,
            roomCode = string.IsNullOrWhiteSpace(roomCode) ? null : roomCode.Trim()
        };

        hasPendingRequest = true;
    }

    public static void SetJoin(string address, ushort port, string fallbackAddress = null, string roomCode = null, string sessionToken = null)
    {
        pendingData = new LaunchData
        {
            mode = LaunchMode.Join,
            address = string.IsNullOrWhiteSpace(address) ? "127.0.0.1" : address.Trim(),
            fallbackAddress = string.IsNullOrWhiteSpace(fallbackAddress) ? null : fallbackAddress.Trim(),
            port = port,
            roomCode = string.IsNullOrWhiteSpace(roomCode) ? null : roomCode.Trim(),
            sessionToken = sessionToken
        };

        hasPendingRequest = true;
    }

    public static bool TryConsume(out LaunchData data)
    {
        data = pendingData;

        if (!hasPendingRequest)
            return false;

        hasPendingRequest = false;
        return true;
    }

    public static void Clear()
    {
        hasPendingRequest = false;
        pendingData = default;
    }
}
