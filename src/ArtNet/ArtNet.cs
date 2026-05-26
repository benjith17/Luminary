using System.Net;
using System.Net.Sockets;

namespace ArtNet;

public class ArtNetSender : IDisposable
{
    private readonly UdpClient _udp;
    private readonly IPEndPoint _target;
    public byte[] Dmx { get; private set; } = new byte[530]; // 18-byte header + 512 DMX channels

    public ArtNetSender(string ip, int universe = 0)
    {
        _udp = new UdpClient();
        _target = new IPEndPoint(IPAddress.Parse(ip), 6454);
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
        try
        {
            _udp.Send(Dmx, Dmx.Length, _target);
        }
        catch (SocketException)
        {
            // TODO: Add error handling and retry logic
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
        Dmx[12] = 0;    // Sequence (0 = disabled)
        Dmx[13] = 0;    // Physical
        Dmx[14] = (byte)(universe & 0xFF); // Universe lo
        Dmx[15] = (byte)(universe >> 8);   // Universe hi
        Dmx[16] = 0x02; // Length hi (512 = 0x0200)
        Dmx[17] = 0x00; // Length lo
    }

    public void Dispose() => _udp.Dispose();
}
