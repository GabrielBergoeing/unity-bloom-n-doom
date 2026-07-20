/// <summary>
/// Global byte counters fed by the transport implementations we control.
/// - PersonalizedTransport reports WIRE bytes (datagrams, including headers,
///   acks, resends and handshake packets).
/// - KcpNgoTransport reports NGO PAYLOAD bytes (kcp2k hides its wire layer).
/// - UnityTransport is not instrumented (counters stay inactive -> -1 in CSV).
/// Cumulative since transport start; the analysis diffs consecutive samples.
/// </summary>
public static class NetTrafficCounter
{
    public static long Sent { get; private set; }
    public static long Received { get; private set; }

    /// <summary>True once any instrumented transport reported traffic.</summary>
    public static bool Active { get; private set; }

    public static void AddSent(int bytes)
    {
        Sent += bytes;
        Active = true;
    }

    public static void AddReceived(int bytes)
    {
        Received += bytes;
        Active = true;
    }

    public static void Reset()
    {
        Sent = 0;
        Received = 0;
        Active = false;
    }
}
