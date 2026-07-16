using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

// Packs a host's LAN address + public address + port into a short, shareable code, so
// players don't have to type/read raw fields. Carrying both addresses lets the client
// try the LAN one first (fast, doesn't depend on NAT/UPnP - covers same-network testing
// and real LAN parties) and fall back to the public one (for friends on another
// network). Not encryption/obfuscation - just a compact, typo-resistant encoding
// (checksum byte) of the same info NetworkLaunchRequest already carries.
public static class JoinCode
{
    // Crockford-style Base32: excludes I/L/O/U to avoid visual ambiguity when read aloud
    // or handwritten.
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
    private const int CodeLength = 18; // ceil(11 bytes * 8 bits / 5 bits per char)

    public static string Encode(IPAddress localAddress, IPAddress publicAddress, ushort port)
    {
        byte[] localBytes = GetIPv4Bytes(localAddress) ?? new byte[4]; // 0.0.0.0 = "no LAN candidate"
        byte[] publicBytes = GetIPv4Bytes(publicAddress)
            ?? throw new ArgumentException("JoinCode requires a valid IPv4 public address.", nameof(publicAddress));

        // 4 bytes local IP + 4 bytes public IP + 2 bytes port (big-endian) + 1 checksum byte.
        byte[] data = new byte[11];
        Array.Copy(localBytes, 0, data, 0, 4);
        Array.Copy(publicBytes, 0, data, 4, 4);
        data[8] = (byte)(port >> 8);
        data[9] = (byte)(port & 0xFF);
        data[10] = Checksum(data); // data[10] is still 0 here, so this XORs only bytes 0..9

        return EncodeBase32(data);
    }

    public static bool TryDecode(string code, out IPAddress localAddress, out IPAddress publicAddress, out ushort port, out string error)
    {
        localAddress = null;
        publicAddress = null;
        port = 0;

        if (string.IsNullOrWhiteSpace(code))
        {
            error = "El código está vacío.";
            return false;
        }

        string cleaned = code.Trim().ToUpperInvariant().Replace("-", "").Replace(" ", "");

        if (cleaned.Length != CodeLength)
        {
            error = $"El código debe tener {CodeLength} caracteres.";
            return false;
        }

        byte[] data;
        try
        {
            data = DecodeBase32(cleaned);
        }
        catch (FormatException)
        {
            error = "El código tiene caracteres inválidos.";
            return false;
        }

        if (data.Length != 11)
        {
            error = "El código tiene una longitud inválida.";
            return false;
        }

        // Checksum() XORs all 11 bytes including the stored checksum itself; for a code
        // that hasn't been mistyped this always collapses to 0 (see Encode()).
        if (Checksum(data) != 0)
        {
            error = "El código no es válido. Revisá que esté bien escrito.";
            return false;
        }

        byte[] localBytes = new byte[4];
        Array.Copy(data, 0, localBytes, 0, 4);
        byte[] publicBytes = new byte[4];
        Array.Copy(data, 4, publicBytes, 0, 4);

        localAddress = new IPAddress(localBytes); // 0.0.0.0 if the host had no LAN candidate
        publicAddress = new IPAddress(publicBytes);
        port = (ushort)((data[8] << 8) | data[9]);

        error = null;
        return true;
    }

    private static byte[] GetIPv4Bytes(IPAddress address)
    {
        if (address == null)
            return null;

        byte[] bytes = address.GetAddressBytes();
        return bytes.Length == 4 ? bytes : null;
    }

    private static byte Checksum(byte[] data)
    {
        byte x = 0;
        for (int i = 0; i < data.Length; i++)
            x ^= data[i];
        return x;
    }

    private static string EncodeBase32(byte[] data)
    {
        var sb = new StringBuilder(CodeLength);
        int bitBuffer = 0;
        int bitCount = 0;

        foreach (byte b in data)
        {
            bitBuffer = (bitBuffer << 8) | b;
            bitCount += 8;

            while (bitCount >= 5)
            {
                bitCount -= 5;
                int index = (bitBuffer >> bitCount) & 0x1F;
                sb.Append(Alphabet[index]);
            }
        }

        if (bitCount > 0)
        {
            int index = (bitBuffer << (5 - bitCount)) & 0x1F;
            sb.Append(Alphabet[index]);
        }

        return sb.ToString();
    }

    private static byte[] DecodeBase32(string text)
    {
        long bitBuffer = 0;
        int bitCount = 0;
        var bytes = new List<byte>(11);

        foreach (char c in text)
        {
            int index = Alphabet.IndexOf(c);
            if (index < 0)
                throw new FormatException($"Invalid character '{c}' in join code.");

            bitBuffer = (bitBuffer << 5) | (uint)index;
            bitCount += 5;

            if (bitCount >= 8)
            {
                bitCount -= 8;
                bytes.Add((byte)((bitBuffer >> bitCount) & 0xFF));
            }
        }

        return bytes.ToArray();
    }
}
