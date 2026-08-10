using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace ArtNet;

// The Art-Net 4 delivery subsystem: owns a socket bound to port 6454, discovers nodes by
// broadcasting ArtPoll and parsing ArtPollReply, and unicasts ArtDmx only to the nodes that
// subscribe to each universe. Manual targets are always unicast to, regardless of discovery.
public sealed class ArtNet4Controller : IDisposable
{
    // Spec default poll addresses are the 2.x and 10.x directed broadcasts; we also poll the limited
    // broadcast so nodes on other subnets (e.g. 192.168.x) are discovered on the local link.
    private static readonly IPEndPoint[] PollTargets =
    [
        new(IPAddress.Parse("2.255.255.255"), ArtNetPackets.Port),
        new(IPAddress.Parse("10.255.255.255"), ArtNetPackets.Port),
        new(IPAddress.Broadcast, ArtNetPackets.Port), // 255.255.255.255
    ];
    private static readonly byte[] PollPacket = ArtNetPackets.BuildArtPoll();

    private readonly UdpClient _socket;
    private readonly NodeTable _nodes = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly CancellationTokenSource _cts = new();
    private readonly System.Timers.Timer? _pollTimer;

    // False if we couldn't bind port 6454 (e.g. another Art-Net app is running): discovery is off
    // and only manual targets receive data.
    public bool DiscoveryEnabled { get; }

    public ArtNet4Controller()
    {
        if (TryBind(out var bound))
        {
            _socket = bound;
            DiscoveryEnabled = true;

            _ = ReceiveLoopAsync(_cts.Token);

            Poll(null, null);                 // poll immediately so discovery starts fast
            _pollTimer = new System.Timers.Timer(2500) { AutoReset = true };
            _pollTimer.Elapsed += Poll;
            _pollTimer.Start();
        }
        else
        {
            // Send-only fallback on an ephemeral port; no discovery.
            _socket = new UdpClient { EnableBroadcast = true };
            DiscoveryEnabled = false;
        }
    }

    /// <summary>
    /// Unicasts a universe's ArtDmx buffer to every subscriber plus any manual targets. Sends
    /// nothing when there are no recipients (broadcast is not permitted in Art-Net 4).
    /// </summary>
    public void Send(int portAddress, byte[] buffer, IReadOnlyList<string> manualTargets, int port)
    {
        var targets = new HashSet<IPAddress>(_nodes.SubscribersFor(portAddress));
        foreach (var t in manualTargets)
            if (IPAddress.TryParse(t, out var ip)) targets.Add(ip);

        foreach (var ip in targets)
            SendTo(buffer, new IPEndPoint(ip, port));
    }

    private void Poll(object? sender, System.Timers.ElapsedEventArgs? e)
    {
        foreach (var target in PollTargets)
            SendTo(PollPacket, target);
        _nodes.Expire(_clock.Elapsed.TotalSeconds);
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var result = await _socket.ReceiveAsync(ct);
                var data = result.Buffer;
                if (ArtNetPackets.ParseArtPollReply(data, data.Length) is { } reply)
                    _nodes.Ingest(reply, _clock.Elapsed.TotalSeconds);
            }
            catch (OperationCanceledException) { break; }
            catch (ObjectDisposedException) { break; }
            catch (SocketException) { /* transient (e.g. ICMP port-unreachable) — keep listening */ }
        }
    }

    private void SendTo(byte[] data, IPEndPoint target)
    {
        try
        {
            _socket.Send(data, data.Length, target);
        }
        catch (SocketException ex)
        {
            Debug.WriteLine($"[ArtNet4] Send to {target} failed: {ex.SocketErrorCode}");
        }
        catch (ObjectDisposedException) { /* shutting down */ }
    }

    private static bool TryBind(out UdpClient socket)
    {
        try
        {
            var s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            s.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            s.EnableBroadcast = true;
            s.Bind(new IPEndPoint(IPAddress.Any, ArtNetPackets.Port));
            socket = new UdpClient { Client = s };
            return true;
        }
        catch (SocketException)
        {
            socket = null!;
            return false;
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _pollTimer?.Stop();
        _pollTimer?.Dispose();
        _socket.Dispose();
        _cts.Dispose();
    }
}
