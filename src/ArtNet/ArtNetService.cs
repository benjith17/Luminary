using System.Timers;
using Model;

namespace ArtNet;

public sealed class ArtNetService : IDisposable
{
    private readonly ShowService _showService;
    private readonly Dictionary<byte, ArtNetSender> _senders = [];
    private readonly System.Timers.Timer _timer;

    public ArtNetService(ShowService showService)
    {
        _showService = showService;

        foreach (var universe in showService.Universes)
            _senders[universe.Number] = new ArtNetSender(universe.TargetIp, universe.Number);

        _timer = new(25); // ~40fps
        _timer.Elapsed += Transmit;
        _timer.AutoReset = true;
        _timer.Start();
    }

    private void Transmit(object? sender, ElapsedEventArgs e)
    {
        foreach (var universe in _showService.Universes)
        {
            if (!_senders.TryGetValue(universe.Number, out var artnetSender))
                continue;

            Array.Copy(universe.Channels, 0, artnetSender.Dmx, 18, 512);
            artnetSender.Send();
        }
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Dispose();
        foreach (var artnetSender in _senders.Values)
            artnetSender.Dispose();
    }
}
