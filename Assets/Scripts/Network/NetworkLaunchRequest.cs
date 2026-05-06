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
        public ushort port;
    }

    private static bool hasPendingRequest;
    private static LaunchData pendingData;

    public static void SetHost(ushort port)
    {
        pendingData = new LaunchData
        {
            mode = LaunchMode.Host,
            address = "127.0.0.1",
            port = port
        };

        hasPendingRequest = true;
    }

    public static void SetJoin(string address, ushort port)
    {
        pendingData = new LaunchData
        {
            mode = LaunchMode.Join,
            address = string.IsNullOrWhiteSpace(address) ? "127.0.0.1" : address.Trim(),
            port = port
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
