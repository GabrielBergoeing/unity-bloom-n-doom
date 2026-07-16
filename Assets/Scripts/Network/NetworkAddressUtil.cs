using System.Net;
using System.Net.Sockets;

// Shared helper for finding this machine's LAN-facing IPv4 address - used both as a
// fallback host address for join codes and as the "internal client" target when asking
// the router to forward a port via UPnP (see UpnpPortMapper).
public static class NetworkAddressUtil
{
    public static string GetLocalIPv4()
    {
        foreach (IPAddress ip in Dns.GetHostAddresses(Dns.GetHostName()))
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                return ip.ToString();
        }

        return null;
    }
}
