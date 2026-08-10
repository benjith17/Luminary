using System.Net;
using System.Timers;
using Model;

namespace ArtNet;

public sealed class ArtNetService : IDisposable
{
    private readonly ShowService _showService;
    private readonly Dictionary<byte, Endpoint> _senders = [];
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
            if (!_senders.TryGetValue(universe.Number, out var endpoint))
                continue;

            Array.Copy(universe.Channels, 0, endpoint.Sender.Dmx, 18, 512);
            endpoint.Sender.Send();
        }
    }

    // Keeps one Art-Net sender per current universe, rebuilding when its settings change and
    // dropping senders for universes that are disabled, invalid, or removed.
    private void Reconcile(IReadOnlyList<Universe> universes)
    {
        foreach (var universe in universes)
        {
            if (universe.Output is ArtNetOutput art && IPAddress.TryParse(art.Ip, out _))
            {
                var key = (art.Ip, art.Port, art.ArtNetUniverse);
                if (_senders.TryGetValue(universe.Number, out var existing) && existing.Key == key)
                    continue;

                existing?.Sender.Dispose();
                _senders[universe.Number] = new Endpoint(new ArtNetSender(art.Ip, art.Port, art.ArtNetUniverse), key);
            }
            else
            {
                Drop(universe.Number);
            }
        }

        var live = universes.Select(u => u.Number).ToHashSet();
        foreach (var number in _senders.Keys.Where(n => !live.Contains(n)).ToList())
            Drop(number);
    }

    private void Drop(byte number)
    {
        if (!_senders.TryGetValue(number, out var endpoint)) return;
        endpoint.Sender.Dispose();
        _senders.Remove(number);
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Dispose();
        foreach (var endpoint in _senders.Values)
            endpoint.Sender.Dispose();
    }

    private sealed record Endpoint(ArtNetSender Sender, (string Ip, int Port, int ArtNetUniverse) Key);
}
