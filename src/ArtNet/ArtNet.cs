using System.Net;
using System.Net.Sockets;

namespace ArtNet;

public class ArtNetSender : IDisposable
{
    private readonly UdpClient _udp;
    private readonly IPEndPoint _target;

    // Per-universe ArtDmx sequence, cycling 1..255 (0 means "sequencing disabled", so it's skipped).
    // Lets receivers detect and reorder out-of-order packets. Only the transmit thread touches this.
    private byte _sequence;

    public byte[] Dmx { get; private set; } = new byte[530]; // 18-byte header + 512 DMX channels

    public ArtNetSender(string ip, int port, int universe)
    {
        _udp = new UdpClient { EnableBroadcast = true };
        _target = new IPEndPoint(IPAddress.Parse(ip), port);
        BuildHeader(universe);
    }

    /// <summary>
    /// Sets a single channel and immediately transmits the full universe.
    /// </summary>
    public void Set(int channel, byte value)
    {
        SetChannel(channel, value);
        Send();
    }

    /// <summary>
    /// Updates a channel in the local buffer without sending.
    /// </summary>
    public void SetChannel(int channel, byte value) =>
        Dmx[18 + channel] = value;

    /// <summary>
    /// Transmits the current DMX buffer to the target node.
    /// </summary>
    public void Send()
    {
        _sequence = _sequence == 255 ? (byte)1 : (byte)(_sequence + 1);
        Dmx[12] = _sequence;

        try
        {
            _udp.Send(Dmx, Dmx.Length, _target);
        }
        catch (SocketException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ArtNet] Send failed: {ex.SocketErrorCode} — {ex.Message}");
        }
    }

    /// <summary>
    /// Zeroes all channels and sends a blackout frame.
    /// </summary>
    public void Blackout()
    {
        Array.Clear(Dmx, 18, 512);
        Send();
    }

    private void BuildHeader(int universe)
    {
        var id = System.Text.Encoding.ASCII.GetBytes("Art-Net\0");
        Array.Copy(id, Dmx, 8);
        Dmx[8]  = 0x00; // OpCode lo (ArtDMX = 0x5000)
        Dmx[9]  = 0x50; // OpCode hi
        Dmx[10] = 0x00; // ProtVer hi
        Dmx[11] = 14;   // ProtVer lo
        Dmx[12] = 0;    // Sequence (set per-send in Send())
        Dmx[13] = 0;    // Physical
        Dmx[14] = (byte)(universe & 0xFF); // Universe lo
        Dmx[15] = (byte)(universe >> 8);   // Universe hi
        Dmx[16] = 0x02; // Length hi (512 = 0x0200)
        Dmx[17] = 0x00; // Length lo
    }

    public void Dispose() => _udp.Dispose();
}
