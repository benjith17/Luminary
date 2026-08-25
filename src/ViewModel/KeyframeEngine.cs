using System.Diagnostics;
using Avalonia.Threading;
using Model;

namespace ViewModel;

// One resolved track: the capability to drive, and a live reference to the keys that drive it.
// The reference is deliberately live — the editor mutates this same list, so edits show up in the
// next frame of a preview without restarting playback.
public sealed record KeyframeTrackTargets(CapabilityViewModelBase Capability, List<Keyframe> Keys);

// Runs a keyframed cue: every track is evaluated independently each frame and the result driven
// onto the playback layer. Serves both show playback (Play, from GO) and the editor (Load, Seek,
// Resume, Pause), because there is only one rig — scrubbing the editor and firing the cue are the
// same act from the fixtures' point of view.
//
// Stopping freezes values rather than releasing them, so the next cue's crossfade eases out of the
// frozen look, matching how chases and snapshot cues hand over.
public sealed class KeyframeEngine
{
    private readonly DispatcherTimer _timer;

    // Wall clock since the last Resume. Sequence time is _origin + _clock.Elapsed, never
    // accumulated frame by frame, so a stalled or coalesced tick can't make playback drift.
    private readonly Stopwatch _clock = new();
    private TimeSpan _origin;

    private List<KeyframeTrackTargets> _tracks = [];
    private byte[]?[] _entryFrom = [];      // live look captured at Play, per track
    private byte[]?[] _lastApplied = [];    // per track, for change detection

    private TimeSpan _duration = TimeSpan.FromSeconds(10);
    private bool _loop;

    // Entry crossfade out of the look that was on stage when the cue fired. Measured in wall time
    // from Play, not sequence time, so it is unaffected by looping or seeking.
    private readonly Stopwatch _entryClock = new();
    private TimeSpan _entryFade;

    public bool IsRunning { get; private set; }
    public bool IsLoaded => _tracks.Count > 0;
    public TimeSpan Duration => _duration;

    // Where the playhead is. Valid whether running, paused or scrubbed.
    public TimeSpan CurrentTime => Clamp(IsRunning ? _origin + _clock.Elapsed : _origin);

    public double Progress => _duration <= TimeSpan.Zero ? 0 : CurrentTime / _duration;

    // Raised on the UI thread whenever the playhead or run state changes.
    public event Action? ProgressChanged;

    public KeyframeEngine()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(25) }; // ~40fps
        _timer.Tick += (_, _) => Tick();
    }

    // Show playback: run from the top, crossfading out of the current live look over entryFade.
    public void Play(IReadOnlyList<KeyframeTrackTargets> tracks, TimeSpan duration, bool loop, TimeSpan entryFade)
    {
        Load(tracks, duration, loop);
        if (_tracks.Count == 0) return;

        // Capture what is on stage now, so the sequence eases in from it rather than snapping.
        if (entryFade > TimeSpan.Zero)
        {
            for (var i = 0; i < _tracks.Count; i++) _entryFrom[i] = _tracks[i].Capability.Capture();
            _entryFade = entryFade;
            _entryClock.Restart();
        }

        // Deliberately not Seek(): seeking is the operator taking hold of the playhead, which
        // cancels the entry crossfade that was just armed. Park at the top and apply directly, so
        // the first frame blends out of the captured look instead of snapping to the sequence.
        _origin = TimeSpan.Zero;
        Apply(TimeSpan.Zero);
        ProgressChanged?.Invoke();

        Resume();
    }

    // Editor: attach to a sequence without running it, so Seek applies exactly what was authored.
    public void Load(IReadOnlyList<KeyframeTrackTargets> tracks, TimeSpan duration, bool loop)
    {
        Stop();

        _tracks = [.. tracks];
        _entryFrom = new byte[]?[_tracks.Count];
        _lastApplied = new byte[]?[_tracks.Count];
        _duration = duration > TimeSpan.Zero ? duration : TimeSpan.FromSeconds(1);
        _loop = loop;
        _entryFade = TimeSpan.Zero;
        _origin = TimeSpan.Zero;
    }

    // Move the playhead and apply that instant. Cancels any entry crossfade: once the operator has
    // taken hold of the playhead they want to see the authored values, not a fade into them.
    public void Seek(TimeSpan time)
    {
        _entryFade = TimeSpan.Zero;
        _origin = Clamp(time);
        if (IsRunning) _clock.Restart();

        Apply(_origin);
        ProgressChanged?.Invoke();
    }

    public void Resume()
    {
        if (_tracks.Count == 0 || IsRunning) return;

        IsRunning = true;
        _clock.Restart();
        _timer.Start();
        ProgressChanged?.Invoke();
    }

    // Holds the playhead where it is, leaving the stage showing that frame.
    public void Pause()
    {
        if (!IsRunning) return;

        _origin = CurrentTime;
        IsRunning = false;
        _timer.Stop();
        _clock.Reset();
        ProgressChanged?.Invoke();
    }

    // Freezes playback and detaches. Values stay exactly where the last frame left them.
    public void Stop()
    {
        _timer.Stop();
        _clock.Reset();
        _entryClock.Reset();
        _entryFade = TimeSpan.Zero;

        var wasLoaded = _tracks.Count > 0 || IsRunning;

        _tracks = []; // don't pin the capability VMs of a show being swapped out
        _entryFrom = [];
        _lastApplied = [];
        _origin = TimeSpan.Zero;
        IsRunning = false;

        if (wasLoaded) ProgressChanged?.Invoke();
    }

    private void Tick()
    {
        var elapsed = _origin + _clock.Elapsed;

        if (elapsed >= _duration)
        {
            if (_loop)
            {
                // Wrap by modulo rather than resetting to zero, so a long frame doesn't lose time.
                var wrapped = TimeSpan.FromTicks(elapsed.Ticks % _duration.Ticks);
                _origin = wrapped;
                _clock.Restart();
                elapsed = wrapped;
            }
            else
            {
                // Run out: settle on the final frame and hold it.
                Apply(_duration);
                _origin = _duration;
                IsRunning = false;
                _timer.Stop();
                _clock.Reset();
                ProgressChanged?.Invoke();
                return;
            }
        }

        Apply(elapsed);
        ProgressChanged?.Invoke();
    }

    // Evaluates every track at this instant and writes the ones that actually changed.
    private void Apply(TimeSpan time)
    {
        var entry = EntryFactor();

        for (var i = 0; i < _tracks.Count; i++)
        {
            var track = _tracks[i];
            var evaluated = Evaluate(track, time);
            if (evaluated is null) continue; // a track with no keys drives nothing

            // Below 1 the sequence is still easing out of the look that was live when it fired.
            // Lerp (not Interpolate) because this leg *is* a crossfade, so Snap parameters jump.
            var final = entry < 1.0 && _entryFrom[i] is { } from
                ? track.Capability.Lerp(from, evaluated, entry)
                : evaluated;

            // Skipping unchanged values is not just an optimisation: every write marks the playback
            // layer as latest, so a track re-asserting a static value each frame would permanently
            // pin its LTP parameters and no fader could ever grab them.
            if (_lastApplied[i] is { } previous && previous.AsSpan().SequenceEqual(final)) continue;

            track.Capability.ApplyValues(final);
            _lastApplied[i] = final;
        }
    }

    private double EntryFactor()
    {
        if (_entryFade <= TimeSpan.Zero) return 1.0;

        var t = _entryClock.Elapsed / _entryFade;
        return t >= 1.0 ? 1.0 : t;
    }

    // The value a track holds at this instant, or null if it has no keys. Before the first key and
    // after the last one the track holds that key's value, so a track only automates the span it
    // was actually written over.
    private static byte[]? Evaluate(KeyframeTrackTargets track, TimeSpan time)
    {
        var keys = track.Keys;
        if (keys.Count == 0) return null;
        if (time <= keys[0].Time) return keys[0].Values;
        if (time >= keys[^1].Time) return keys[^1].Values;

        var index = SegmentAt(keys, time);
        var a = keys[index];
        var b = keys[index + 1];

        if (a.Interpolation == Interpolation.Hold) return a.Values;

        var span = b.Time - a.Time;
        if (span <= TimeSpan.Zero) return b.Values;

        return track.Capability.Interpolate(a.Values, b.Values, Ease((time - a.Time) / span, a.Interpolation));
    }

    // Index of the last key at or before time. Binary search, so a track with hundreds of keys
    // costs the same per frame as one with two.
    private static int SegmentAt(List<Keyframe> keys, TimeSpan time)
    {
        int low = 0, high = keys.Count - 1;

        while (low < high)
        {
            var mid = (low + high + 1) / 2;
            if (keys[mid].Time <= time) low = mid;
            else high = mid - 1;
        }

        return low;
    }

    private static double Ease(double t, Interpolation interpolation) => interpolation switch
    {
        Interpolation.EaseIn => t * t,
        Interpolation.EaseOut => t * (2 - t),
        Interpolation.EaseInOut => t * t * (3 - 2 * t),
        _ => t // Linear; Hold never reaches here
    };

    private TimeSpan Clamp(TimeSpan time) =>
        time < TimeSpan.Zero ? TimeSpan.Zero : time > _duration ? _duration : time;
}
