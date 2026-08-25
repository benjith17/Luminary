using System.Diagnostics;
using Avalonia.Threading;

namespace ViewModel;

// One resolved chase step: the capability targets to drive, and how long the chase rests here.
public sealed record ChaseStepTargets(
    IReadOnlyList<(CapabilityViewModelBase Capability, byte[] Target)> Targets,
    TimeSpan Duration);

// Loops a chase cue's steps until stopped. Each step crossfades from whatever is live to the
// step's values, then holds for the rest of its duration before advancing; the last step wraps
// back to the first. Drives the playback layer via ApplyLerp, exactly like CrossfadeEngine, so a
// chase and a snapshot cue are the same kind of source as far as the merge stack is concerned.
//
// Stopping leaves every value where it stands rather than releasing it. That is what makes GO on
// the next cue behave: CrossfadeEngine captures its start point from the live output, so the
// incoming cue fades out of the frozen chase look instead of snapping.
public sealed class ChaseEngine
{
    // A step shorter than one tick would spin the chase at frame rate; floor it.
    private static readonly TimeSpan MinStepDuration = TimeSpan.FromMilliseconds(25);

    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _clock = new();

    private List<ChaseStepTargets> _steps = [];
    private List<FadeChannel> _channels = [];
    private TimeSpan _stepFade;     // fade into every step after the first
    private TimeSpan _fade;         // fade into the current step (entry fade for the first)
    private TimeSpan _duration;     // current step's total duration
    private bool _settled;          // true once the current step's fade has reached its target

    // Live state for the transport strip.
    public bool IsRunning { get; private set; }
    public int StepIndex { get; private set; }
    public int StepCount => _steps.Count;
    public double StepProgress { get; private set; }   // 0..1 through the current step

    // Raised on the UI thread when the chase starts, advances a step, or ticks.
    public event Action? ProgressChanged;

    public ChaseEngine()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(25) }; // ~40fps
        _timer.Tick += (_, _) => Tick();
    }

    // Starts (or restarts) a chase. entryFade is the crossfade into the first step — the cue's own
    // fade time — after which stepFade applies to every subsequent step.
    public void Start(IReadOnlyList<ChaseStepTargets> steps, TimeSpan entryFade, TimeSpan stepFade)
    {
        Stop();
        if (steps.Count == 0) return;

        _steps = [.. steps];
        _stepFade = stepFade > TimeSpan.Zero ? stepFade : TimeSpan.Zero;

        IsRunning = true;
        EnterStep(0, entryFade);
        _timer.Start();
    }

    // Freezes the chase where it is. Values stay on the playback layer untouched.
    public void Stop()
    {
        _timer.Stop();
        _clock.Reset();
        _channels = [];
        _steps = []; // don't pin the capability VMs of a show that's being swapped out

        if (!IsRunning && StepProgress == 0.0 && StepIndex == 0) return;

        IsRunning = false;
        StepIndex = 0;
        StepProgress = 0.0;
        ProgressChanged?.Invoke();
    }

    // Captures the live output as the fade start, so each step eases out of whatever is on stage —
    // including a step interrupted mid-fade by a restart.
    private void EnterStep(int index, TimeSpan fade)
    {
        var step = _steps[index];

        StepIndex = index;
        StepProgress = 0.0;
        _duration = step.Duration > MinStepDuration ? step.Duration : MinStepDuration;

        // A fade longer than the step would never finish before the chase moved on.
        _fade = fade > _duration ? _duration : fade;
        _settled = false;

        _channels = [.. step.Targets.Select(t => new FadeChannel(t.Capability, t.Capability.Capture(), t.Target))];

        if (_fade <= TimeSpan.Zero)
        {
            Apply(1.0); // hard step
            _settled = true;
        }
        else
        {
            Apply(0.0); // snap properties jump now; fade properties hold at their start value
        }

        _clock.Restart();
        ProgressChanged?.Invoke();
    }

    private void Tick()
    {
        var elapsed = _clock.Elapsed;

        if (elapsed >= _duration)
        {
            // Wrap round the step list — a chase runs until something stops it.
            EnterStep((StepIndex + 1) % _steps.Count, _stepFade);
            return;
        }

        if (!_settled)
        {
            if (elapsed >= _fade)
            {
                Apply(1.0);
                _settled = true;
            }
            else
            {
                Apply(elapsed / _fade);
            }
        }

        // Once settled the step just holds. Deliberately no further writes: re-applying the target
        // every frame would keep marking the playback layer as latest and pin every LTP parameter,
        // so a fader could never grab a colour or position mid-chase.
        StepProgress = elapsed / _duration;
        ProgressChanged?.Invoke();
    }

    private void Apply(double t)
    {
        foreach (var channel in _channels)
            channel.Capability.ApplyLerp(channel.From, channel.To, t);
    }

    private readonly record struct FadeChannel(CapabilityViewModelBase Capability, byte[] From, byte[] To);
}
