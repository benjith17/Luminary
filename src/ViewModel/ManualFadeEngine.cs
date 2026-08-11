using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Avalonia.Threading;

namespace ViewModel;

// Fades the manual (fader) layer of capability parameters over time, for macro commands like
// `L2 @ 80% fade 3s`. Unlike the cue CrossfadeEngine (which drives the playback layer), this drives
// Manual. Any number of parameters can fade at once; starting a new fade on a parameter replaces an
// existing one, and an instant set cancels its fade. Runs on the UI thread at ~40fps.
public sealed class ManualFadeEngine
{
    private readonly DispatcherTimer _timer;
    private readonly Dictionary<CapabilityParameter, Fade> _active = new();

    public ManualFadeEngine()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(25) };
        _timer.Tick += (_, _) => Tick();
    }

    // Fade a parameter's manual value from wherever it is now to target over duration. A zero (or
    // negative) duration is an instant set.
    public void Start(CapabilityParameter parameter, int target, TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            _active.Remove(parameter);
            parameter.Manual = target;
            return;
        }

        _active[parameter] = new Fade(parameter.Manual, target, Stopwatch.StartNew(), duration);
        if (!_timer.IsEnabled) _timer.Start();
    }

    // Drop any in-progress fade on this parameter (used before an instant set).
    public void Cancel(CapabilityParameter parameter) => _active.Remove(parameter);

    // Stop every fade and leave parameters at their current value (used when a macro is stopped).
    public void CancelAll()
    {
        _active.Clear();
        _timer.Stop();
    }

    private void Tick()
    {
        // Snapshot because completed fades are removed from the dictionary during iteration.
        foreach (var (parameter, fade) in _active.ToList())
        {
            var t = fade.Clock.Elapsed / fade.Duration;
            if (t >= 1.0)
            {
                parameter.Manual = fade.To;
                _active.Remove(parameter);
            }
            else
            {
                parameter.Manual = (int)System.Math.Round(fade.From + (fade.To - fade.From) * t);
            }
        }

        if (_active.Count == 0) _timer.Stop();
    }

    private sealed record Fade(int From, int To, Stopwatch Clock, TimeSpan Duration);
}
