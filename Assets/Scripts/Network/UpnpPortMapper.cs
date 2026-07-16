using System;
using System.Collections;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using UnityEngine;
using UnityEngine.Networking;

// Minimal UPnP IGD (Internet Gateway Device) client, no external dependency. Best-effort:
// asks the LAN router to forward an external UDP port straight to this machine, so the
// P2P "host" method works for friends over the internet without the host manually
// configuring port forwarding. Works on most consumer routers with UPnP enabled (on by
// default on many of them); does nothing useful on routers with UPnP disabled or behind
// carrier-grade NAT (CGNAT) - those still need manual forwarding, a relay, or a
// dedicated server (see GameLiftConnectionProvider for that future path).
public static class UpnpPortMapper
{
    private const string SsdpAddress = "239.255.255.250";
    private const int SsdpPort = 1900;
    private const string SearchTarget = "urn:schemas-upnp-org:device:InternetGatewayDevice:1";
    private const int DiscoveryTimeoutMs = 3000;

    private class GatewayInfo
    {
        public string ControlUrl;
        public string ServiceType;
    }

    public static IEnumerator TryMapPort(ushort port, string description, Action<bool> onComplete = null)
    {
        GatewayInfo gateway = null;
        yield return DiscoverGateway(g => gateway = g);

        if (gateway == null)
        {
            Debug.LogWarning("[UPnP] No se encontró un router compatible, o UPnP está desactivado. El host podría necesitar port forwarding manual.");
            onComplete?.Invoke(false);
            yield break;
        }

        string localIp = NetworkAddressUtil.GetLocalIPv4();
        if (string.IsNullOrEmpty(localIp))
        {
            Debug.LogWarning("[UPnP] No se pudo determinar la IP local para mapear el puerto.");
            onComplete?.Invoke(false);
            yield break;
        }

        string body = BuildEnvelope(gateway.ServiceType, "AddPortMapping",
            "<NewRemoteHost></NewRemoteHost>" +
            $"<NewExternalPort>{port}</NewExternalPort>" +
            "<NewProtocol>UDP</NewProtocol>" +
            $"<NewInternalPort>{port}</NewInternalPort>" +
            $"<NewInternalClient>{localIp}</NewInternalClient>" +
            "<NewEnabled>1</NewEnabled>" +
            $"<NewPortMappingDescription>{description}</NewPortMappingDescription>" +
            "<NewLeaseDuration>0</NewLeaseDuration>");

        using (UnityWebRequest request = BuildSoapRequest(gateway.ControlUrl, gateway.ServiceType, "AddPortMapping", body))
        {
            yield return request.SendWebRequest();

            bool success = request.result == UnityWebRequest.Result.Success;
            if (success)
                Debug.Log($"[UPnP] Puerto UDP {port} mapeado automáticamente en el router.");
            else
                Debug.LogWarning($"[UPnP] El router rechazó el mapeo de puerto ({request.error}). Puede necesitar forwarding manual.");

            onComplete?.Invoke(success);
        }
    }

    public static IEnumerator TryUnmapPort(ushort port)
    {
        GatewayInfo gateway = null;
        yield return DiscoverGateway(g => gateway = g);

        if (gateway == null)
            yield break;

        string body = BuildEnvelope(gateway.ServiceType, "DeletePortMapping",
            "<NewRemoteHost></NewRemoteHost>" +
            $"<NewExternalPort>{port}</NewExternalPort>" +
            "<NewProtocol>UDP</NewProtocol>");

        using (UnityWebRequest request = BuildSoapRequest(gateway.ControlUrl, gateway.ServiceType, "DeletePortMapping", body))
            yield return request.SendWebRequest();
    }

    // SSDP discovery needs a blocking UDP receive, so it runs on a background thread;
    // the description-XML fetch uses UnityWebRequest, which must run on the main thread -
    // this coroutine bridges the two and hands back a GatewayInfo (or null) via callback.
    private static IEnumerator DiscoverGateway(Action<GatewayInfo> onDiscovered)
    {
        Task<string> locationTask = Task.Run(() => DiscoverGatewayLocationUrl());
        while (!locationTask.IsCompleted)
            yield return null;

        string locationUrl = locationTask.Result;
        if (string.IsNullOrEmpty(locationUrl))
        {
            onDiscovered(null);
            yield break;
        }

        using (UnityWebRequest descriptionRequest = UnityWebRequest.Get(locationUrl))
        {
            yield return descriptionRequest.SendWebRequest();

            GatewayInfo gateway = descriptionRequest.result == UnityWebRequest.Result.Success
                ? ParseGatewayInfo(descriptionRequest.downloadHandler.text, locationUrl)
                : null;

            onDiscovered(gateway);
        }
    }

    private static string DiscoverGatewayLocationUrl()
    {
        try
        {
            using (var udp = new UdpClient(AddressFamily.InterNetwork))
            {
                udp.Client.ReceiveTimeout = DiscoveryTimeoutMs;

                string search =
                    "M-SEARCH * HTTP/1.1\r\n" +
                    $"HOST: {SsdpAddress}:{SsdpPort}\r\n" +
                    "MAN: \"ssdp:discover\"\r\n" +
                    "MX: 2\r\n" +
                    $"ST: {SearchTarget}\r\n\r\n";

                byte[] searchBytes = Encoding.ASCII.GetBytes(search);
                udp.Send(searchBytes, searchBytes.Length, new IPEndPoint(IPAddress.Parse(SsdpAddress), SsdpPort));

                var remote = new IPEndPoint(IPAddress.Any, 0);
                byte[] response = udp.Receive(ref remote);
                return ParseHeader(Encoding.ASCII.GetString(response), "LOCATION");
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[UPnP] Descubrimiento de router falló: {ex.Message}");
            return null;
        }
    }

    private static string ParseHeader(string httpMessage, string headerName)
    {
        foreach (string line in httpMessage.Split(new[] { "\r\n" }, StringSplitOptions.None))
        {
            int colon = line.IndexOf(':');
            if (colon <= 0)
                continue;

            if (string.Equals(line.Substring(0, colon).Trim(), headerName, StringComparison.OrdinalIgnoreCase))
                return line.Substring(colon + 1).Trim();
        }

        return null;
    }

    private static GatewayInfo ParseGatewayInfo(string descriptionXml, string descriptionUrl)
    {
        XDocument doc;
        try
        {
            doc = XDocument.Parse(descriptionXml);
        }
        catch (Exception)
        {
            return null;
        }

        // Ignore XML namespaces here on purpose - real-world router firmware is
        // inconsistent about declaring them, matching by local name is more forgiving.
        foreach (XElement service in doc.Descendants().Where(e => e.Name.LocalName == "service"))
        {
            string serviceType = service.Elements().FirstOrDefault(e => e.Name.LocalName == "serviceType")?.Value;
            if (string.IsNullOrEmpty(serviceType))
                continue;

            if (!serviceType.Contains("WANIPConnection") && !serviceType.Contains("WANPPPConnection"))
                continue;

            string controlPath = service.Elements().FirstOrDefault(e => e.Name.LocalName == "controlURL")?.Value;
            if (string.IsNullOrEmpty(controlPath))
                continue;

            Uri controlUri = new Uri(new Uri(descriptionUrl), controlPath);
            return new GatewayInfo { ControlUrl = controlUri.ToString(), ServiceType = serviceType };
        }

        return null;
    }

    private static string BuildEnvelope(string serviceType, string action, string argumentsXml)
    {
        return
            "<?xml version=\"1.0\"?>" +
            "<s:Envelope xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\" s:encodingStyle=\"http://schemas.xmlsoap.org/soap/encoding/\">" +
            "<s:Body>" +
            $"<u:{action} xmlns:u=\"{serviceType}\">{argumentsXml}</u:{action}>" +
            "</s:Body>" +
            "</s:Envelope>";
    }

    private static UnityWebRequest BuildSoapRequest(string controlUrl, string serviceType, string action, string body)
    {
        byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
        var request = new UnityWebRequest(controlUrl, "POST")
        {
            uploadHandler = new UploadHandlerRaw(bodyBytes),
            downloadHandler = new DownloadHandlerBuffer()
        };
        request.SetRequestHeader("Content-Type", "text/xml; charset=\"utf-8\"");
        request.SetRequestHeader("SOAPAction", $"\"{serviceType}#{action}\"");
        return request;
    }
}
