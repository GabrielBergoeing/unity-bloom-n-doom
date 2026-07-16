using System.Net;

// Today's only active IConnectionProvider: decodes the short code the host shared.
public class JoinCodeConnectionProvider : IConnectionProvider
{
    public bool TryRequestConnection(string userInput, out ConnectionInfo info, out string error)
    {
        info = default;

        if (!JoinCode.TryDecode(userInput, out IPAddress local, out IPAddress publicAddress, out ushort port, out error))
            return false;

        bool hasLocal = !IPAddress.Any.Equals(local);
        bool hasPublic = !IPAddress.Any.Equals(publicAddress);

        if (!hasLocal && !hasPublic)
        {
            error = "El código no contiene ninguna dirección válida.";
            return false;
        }

        // Prefer the LAN address first (fast, works without depending on NAT/UPnP);
        // the public address is the fallback for real internet play. If the host
        // couldn't detect its own LAN IP, just use the public one as the only candidate.
        string primary = hasLocal ? local.ToString() : publicAddress.ToString();
        string fallback = hasLocal && hasPublic && !local.Equals(publicAddress) ? publicAddress.ToString() : null;

        info = new ConnectionInfo
        {
            address = primary,
            fallbackAddress = fallback,
            port = port,
            sessionToken = null
        };
        return true;
    }
}
