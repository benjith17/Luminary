using System.Net;
using System.Timers;
using Model;

namespace ArtNet;

public sealed class ArtNetService : IDisposable
{
    private readonly ShowService _showService;
    private readonly Dictionary<byte, Endpoint> _senders = [];   // Art-Net 3 (broadcast) per universe
    private readonly Dictionary<byte, V4Buffer> _v4 = [];        // Art-Net 4 per-universe frame buffer
    private ArtNet4Controller? _v4Controller;                    // shared v4 discovery/unicast subsystem
    private readonly System.Timers.Timer _timer;

    public ArtNetService(ShowService showService)
    {
        _showService = showService;

        _timer = new(25); // ~40fps
        _timer.Elapsed += Transmit;
        _timer.AutoReset = true;
        _timer.Start();
    }

    private void Transmit(object? sender, ElapsedEventArgs e)
    {
        // Capture the reference once. Universe add/remove replaces the list wholesale (copy-on-
        // write), so this stays a stable snapshot even though the UI thread mutates the show.
        var universes = _showService.Universes;

        Reconcile(universes);

        foreach (var universe in universes)
        {
            switch (universe.Output)
            {
                case ArtNetOutput when _senders.TryGetValue(universe.Number, out var endpoint):
                    Array.Copy(universe.Channels, 0, endpoint.Sender.Dmx, 18, 512);
                    endpoint.Sender.Send();
                    break;

                case ArtNet4Output v4 when _v4Controller is { } controller && _v4.TryGetValue(universe.Number, out var buffer):
                    buffer.Load(universe.Channels);
                    controller.Send(v4.ArtNetUniverse, buffer.Data, v4.ManualTargets, v4.Port);
                    break;
            }
        }
    }

    // Keeps the per-universe senders/buffers and the v4 controller in step with the show: creating,
    // rebuilding on settings change, and dropping them for universes that are disabled or removed.
    private void Reconcile(IReadOnlyList<Universe> universes)
    {
        foreach (var universe in universes)
        {
            switch (universe.Output)
            {
                case ArtNetOutput art when IPAddress.TryParse(art.Ip, out _):
                {
                    DropV4(universe.Number);
                    var key = (art.Ip, art.Port, art.ArtNetUniverse);
                    if (_senders.TryGetValue(universe.Number, out var existing) && existing.Key == key)
                        break;
                    existing?.Sender.Dispose();
                    _senders[universe.Number] = new Endpoint(new ArtNetSender(art.Ip, art.Port, art.ArtNetUniverse), key);
                    break;
                }

                case ArtNet4Output v4:
                {
                    DropSender(universe.Number);
                    _v4Controller ??= new ArtNet4Controller();
                    if (_v4.TryGetValue(universe.Number, out var buffer) && buffer.PortAddress == v4.ArtNetUniverse)
                        break;
                    _v4[universe.Number] = new V4Buffer(v4.ArtNetUniverse);
                    break;
                }

                default: // disabled, or an Art-Net output with an unparseable IP
                    DropSender(universe.Number);
                    DropV4(universe.Number);
                    break;
            }
        }

        var live = universes.Select(u => u.Number).ToHashSet();
        foreach (var number in _senders.Keys.Where(n => !live.Contains(n)).ToList())
            DropSender(number);
        foreach (var number in _v4.Keys.Where(n => !live.Contains(n)).ToList())
            DropV4(number);

        // No universe needs discovery anymore — release the socket bound to port 6454.
        if (_v4.Count == 0 && _v4Controller is { } controller)
        {
            controller.Dispose();
            _v4Controller = null;
        }
    }

    private void DropSender(byte number)
    {
        if (_senders.Remove(number, out var endpoint))
            endpoint.Sender.Dispose();
    }

    private void DropV4(byte number) => _v4.Remove(number);

    public void Dispose()
    {
        _timer.Stop();
        _timer.Dispose();
        foreach (var endpoint in _senders.Values)
            endpoint.Sender.Dispose();
        _v4Controller?.Dispose();
    }

    private sealed record Endpoint(ArtNetSender Sender, (string Ip, int Port, int ArtNetUniverse) Key);

    // A per-universe Art-Net 4 frame: the 530-byte ArtDmx buffer plus its own sequence counter.
    private sealed class V4Buffer
    {
        public byte[] Data { get; } = new byte[530];
        public int PortAddress { get; }
        private byte _sequence;

        public V4Buffer(int portAddress)
        {
            PortAddress = portAddress;
            ArtNetPackets.WriteDmxHeader(Data, portAddress);
        }

        public void Load(byte[] channels)
        {
            Array.Copy(channels, 0, Data, 18, 512);
            _sequence = _sequence == 255 ? (byte)1 : (byte)(_sequence + 1);
            Data[12] = _sequence;
        }
    }
}
