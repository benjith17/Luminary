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

    // Live fade state, for a progress readout. IsFading is false between fades and for hard cuts.
    public bool IsFading { get; private set; }
    public double Progress { get; private set; }        // 0..1 through the current fade
    public TimeSpan Duration => _duration;
    public TimeSpan Elapsed => _clock.Elapsed < _duration ? _clock.Elapsed : _duration;

    // Raised on the UI thread whenever IsFading / Progress change (fade start, each frame, and end).
    public event Action? ProgressChanged;

    public CrossfadeEngine()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(25) }; // ~40fps
        _timer.Tick += (_, _) => Tick();
    }

    // Starts (or replaces) a fade. The start state is captured now from the live values, so
    // re-firing mid-fade continues smoothly from wherever the previous fade had reached.
    public void Start(IEnumerable<(CapabilityViewModelBase Capability, byte[] Target)> targets, TimeSpan duration)
    {
        // Fade each parameter's playback layer from the current on-stage output to the cue
        // value. Starting from Output (not the raw playback layer) means LTP channels fade down
        // from whatever is currently showing instead of snapping, and re-firing continues smoothly.
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

        IsFading = true;
        Progress = 0.0;
        ProgressChanged?.Invoke();

        Apply(0.0); // snap properties jump immediately; fade properties hold at their start value
        _clock.Restart();
        _timer.Start();
    }

    public void Stop()
    {
        _timer.Stop();
        _clock.Reset();

        if (IsFading || Progress != 0.0)
        {
            IsFading = false;
            Progress = 0.0;
            ProgressChanged?.Invoke();
        }
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
        Progress = t;
        ProgressChanged?.Invoke();
    }

    private void Apply(double t)
    {
        foreach (var channel in _channels)
            channel.Capability.ApplyLerp(channel.From, channel.To, t);
    }

    private readonly record struct FadeChannel(CapabilityViewModelBase Capability, byte[] From, byte[] To);
}
