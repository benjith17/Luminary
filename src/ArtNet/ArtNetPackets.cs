using System.Net;

namespace ArtNet;

// Pure packet formatting / parsing for Art-Net 4 discovery. No sockets here so the byte-level
// logic (offsets, addressing) can be reasoned about and tested in isolation.
public static class ArtNetPackets
{
    public const int Port = 6454;

    public const int OpPoll = 0x2000;
    public const int OpPollReply = 0x2100;
    public const int OpDmx = 0x5000;

    private static readonly byte[] Id = System.Text.Encoding.ASCII.GetBytes("Art-Net\0");

    // ArtPoll flag bits (field "Flags").
    public const byte PollFlagReplyOnChange = 0x02; // node also sends ArtPollReply when its state changes

    // Identity we advertise in ArtPollReply.
    private const string ShortName = "Luminary";
    private const string LongName = "Luminary Lighting Console";

    /// <summary>
    /// The Art-Net OpCode of a packet, or -1 if it isn't a valid Art-Net packet.
    /// </summary>
    public static int OpCodeOf(byte[] data, int length) =>
        length < 10 || !HasArtNetId(data) ? -1 : data[8] | (data[9] << 8);

    /// <summary>
    /// Builds an ArtPoll packet used to discover nodes and their subscribed universes.
    /// </summary>
    public static byte[] BuildArtPoll(byte flags = PollFlagReplyOnChange)
    {
        var p = new byte[22];
        Array.Copy(Id, p, 8);
        p[8] = OpPoll & 0xFF;         // OpCode low byte first
        p[9] = (OpPoll >> 8) & 0xFF;
        p[10] = 0;                    // ProtVerHi
        p[11] = 14;                   // ProtVerLo
        p[12] = flags;
        p[13] = 0;                    // DiagPriority
        // 14–17 TargetPortAddress Top/Bottom (0 = targeted mode off)
        // 18–19 EstaMan, 20–21 Oem: left 0 (placeholder — register an OEM code before release)
        return p;
    }

    /// <summary>
    /// Writes the 18-byte ArtDmx header (for a 512-channel universe) into <paramref name="buffer"/>.
    /// </summary>
    public static void WriteDmxHeader(byte[] buffer, int portAddress)
    {
        Array.Copy(Id, buffer, 8);
        buffer[8] = OpDmx & 0xFF;          // OpCode low byte first
        buffer[9] = (OpDmx >> 8) & 0xFF;
        buffer[10] = 0;                    // ProtVerHi
        buffer[11] = 14;                   // ProtVerLo
        buffer[12] = 0;                    // Sequence (stamped per-send)
        buffer[13] = 0;                    // Physical
        buffer[14] = (byte)(portAddress & 0xFF); // SubUni (low 8 bits of Port-Address)
        buffer[15] = (byte)(portAddress >> 8);   // Net (top 7 bits)
        buffer[16] = 0x02;                 // LengthHi (512 = 0x0200)
        buffer[17] = 0x00;                 // LengthLo
    }

    /// <summary>
    /// Parses an ArtPollReply into the node's IP and the set of universes it subscribes to.
    /// Returns null if the packet isn't a valid ArtPollReply or is too short to read subscriptions.
    /// </summary>
    public static ParsedReply? ParseArtPollReply(byte[] data, int length)
    {
        // Need through SwOut[3] at offset 193 to read subscriptions.
        if (length < 194 || !HasArtNetId(data)) return null;
        if ((data[8] | (data[9] << 8)) != OpPollReply) return null;

        var ip = new IPAddress([data[10], data[11], data[12], data[13]]);
        int net = data[18] & 0x7F;   // NetSwitch: Port-Address bits 14–8
        int sub = data[19] & 0x0F;   // SubSwitch: Port-Address bits 7–4
        int baseAddr = (net << 8) | (sub << 4);
        int numPorts = Math.Min(data[173], (byte)4); // NumPortsLo
        int bindIndex = length > 211 ? data[211] : 0; // may be absent in a short (>=207) reply

        var universes = new HashSet<int>();
        for (int i = 0; i < numPorts; i++)
        {
            byte type = data[174 + i]; // PortTypes[i]
            if ((type & 0x80) != 0)    // bit 7: outputs Art-Net → DMX (SwOut)
                universes.Add(baseAddr | (data[190 + i] & 0x0F));
            if ((type & 0x40) != 0)    // bit 6: inputs DMX → Art-Net (SwIn)
                universes.Add(baseAddr | (data[186 + i] & 0x0F));
        }

        return new ParsedReply(ip, bindIndex, universes);
    }

    /// <summary>
    /// Builds an ArtPollReply advertising this device and up to four universes that all share the
    /// same Net and Sub-Net. The universes are reported as output ports so discovery tools associate
    /// us with them. Callers with universes across multiple Net/Sub-Nets send one reply per group,
    /// incrementing <paramref name="bindIndex"/>.
    /// </summary>
    public static byte[] BuildArtPollReply(IPAddress localIp, int net, int subNet, IReadOnlyList<int> lowNibbles, int bindIndex)
    {
        var p = new byte[239];
        Array.Copy(Id, p, 8);
        p[8] = OpPollReply & 0xFF;          // OpCode low byte first
        p[9] = (OpPollReply >> 8) & 0xFF;

        var ip = localIp.GetAddressBytes();
        Array.Copy(ip, 0, p, 10, 4);        // IP Address
        p[14] = Port & 0xFF;                // Port 0x1936, low byte first
        p[15] = (Port >> 8) & 0xFF;
        p[16] = 0;                          // VersInfoH
        p[17] = 1;                          // VersInfoL
        p[18] = (byte)(net & 0x7F);         // NetSwitch
        p[19] = (byte)(subNet & 0x0F);      // SubSwitch
        p[23] = 0xC0;                       // Status1: indicators in Normal mode

        WriteString(p, 26, ShortName, 18);  // PortName
        WriteString(p, 44, LongName, 64);   // LongName
        WriteString(p, 108, "#0001 [0000] OK", 64); // NodeReport

        int ports = Math.Min(lowNibbles.Count, 4);
        p[173] = (byte)ports;               // NumPortsLo
        for (int i = 0; i < ports; i++)
        {
            p[174 + i] = 0x80;              // PortTypes: output, DMX512
            p[182 + i] = 0x80;              // GoodOutputA: data is being output
            p[190 + i] = (byte)(lowNibbles[i] & 0x0F); // SwOut
        }

        p[200] = 0x01;                      // Style: StController
        Array.Copy(ip, 0, p, 207, 4);       // BindIp (root device)
        p[211] = (byte)bindIndex;           // BindIndex (1 = root)
        p[212] = 0x08;                      // Status2: supports 15-bit Port-Address (Art-Net 3/4)
        p[227] = 44;                        // RefreshRateLo: 44 Hz (max DMX512)
        return p;
    }

    private static void WriteString(byte[] buffer, int offset, string value, int fieldLength)
    {
        var bytes = System.Text.Encoding.ASCII.GetBytes(value);
        int n = Math.Min(bytes.Length, fieldLength - 1); // leave room for the null terminator
        Array.Copy(bytes, 0, buffer, offset, n);
        buffer[offset + n] = 0;
    }

    private static bool HasArtNetId(byte[] data)
    {
        for (int i = 0; i < 8; i++)
            if (data[i] != Id[i]) return false;
        return true;
    }
}

// A node's identity plus the universes it wants, extracted from one ArtPollReply.
public sealed record ParsedReply(IPAddress Ip, int BindIndex, HashSet<int> SubscribedUniverses);
