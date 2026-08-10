using System.Diagnostics;
using Avalonia.Threading;

namespace ViewModel;

// Drives a single active crossfade. Each frame it interpolates every target capability from
// the value it held when the fade started toward the cue's value, on the UI thread — so the
// capability VMs (which are UI-bound and push to the DMX universe) update safely.
public sealed class CrossfadeEngine
{
    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _clock = new();
    private List<FadeChannel> _channels = [];
    private TimeSpan _duration;

    public CrossfadeEngine()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(25) }; // ~40fps
        _timer.Tick += (_, _) => Tick();
    }

    // Starts (or replaces) a fade. The start state is captured now from the live values, so
    // re-firing mid-fade continues smoothly from wherever the previous fade had reached.
    public void Start(IEnumerable<(CapabilityViewModelBase Capability, byte[] Target)> targets, TimeSpan duration)
    {
        _channels = targets
            .Select(x => new FadeChannel(x.Capability, x.Capability.Capture(), x.Target))
            .ToList();
        _duration = duration;

        if (duration <= TimeSpan.Zero)
        {
            Apply(1.0); // hard cut
            Stop();
            return;
        }

        Apply(0.0); // snap properties jump immediately; fade properties hold at their start value
        _clock.Restart();
        _timer.Start();
    }

    public void Stop()
    {
        _timer.Stop();
        _clock.Reset();
    }

    private void Tick()
    {
        var t = _clock.Elapsed / _duration;
        if (t >= 1.0)
        {
            Apply(1.0);
            Stop();
            return;
        }

        Apply(t);
    }

    private void Apply(double t)
    {
        foreach (var channel in _channels)
            channel.Capability.ApplyLerp(channel.From, channel.To, t);
    }

    private readonly record struct FadeChannel(CapabilityViewModelBase Capability, byte[] From, byte[] To);
}
