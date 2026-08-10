using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ViewModel;

// How two sources of a value combine into the DMX output.
public enum MergeMode
{
    Htp, // Highest Takes Precedence — output is the larger of the two layers (classic for intensity)
    Ltp  // Latest Takes Precedence — whichever layer changed most recently wins (classic for colour/position)
}

// One controllable value of a capability, split into a manual layer (the slider/pad) and a
// playback layer (cue recall / crossfade). The two merge into Output, which is what reaches DMX.
public partial class CapabilityParameter : ObservableObject
{
    private readonly Action<int> _writeOutput;
    private bool _manualIsLatest = true;

    public int Max { get; }
    public int Width { get; }        // bytes in a cue snapshot: 1 (8-bit) or 2 (16-bit)
    public MergeMode Merge { get; }
    public FadeBehavior Fade { get; }

    public CapabilityParameter(int max, int width, MergeMode merge, FadeBehavior fade, Action<int> writeOutput)
    {
        Max = max;
        Width = width;
        Merge = merge;
        Fade = fade;
        _writeOutput = writeOutput;
    }

    // Manual layer — driven by the slider / XY pad.
    [ObservableProperty]
    public partial int Manual { get; set; }

    // Playback layer — driven by cue recall / crossfade.
    [ObservableProperty]
    public partial int Playback { get; set; }

    // True once a cue has driven the playback layer; used to hide the cue indicator when no
    // cue is contributing (as opposed to a cue genuinely holding this value at 0).
    [ObservableProperty]
    public partial bool PlaybackActive { get; set; }

    // What actually goes to DMX.
    public int Output => Merge == MergeMode.Htp
        ? Math.Max(Manual, Playback)
        : _manualIsLatest ? Manual : Playback;

    public double ManualFraction => Max == 0 ? 0 : (double)Manual / Max;
    public double PlaybackFraction => Max == 0 ? 0 : (double)Playback / Max;

    partial void OnManualChanged(int value)
    {
        _manualIsLatest = true;
        Push();
        OnPropertyChanged(nameof(ManualFraction));
    }

    partial void OnPlaybackChanged(int value)
    {
        _manualIsLatest = false;
        Push();
        OnPropertyChanged(nameof(PlaybackFraction));
    }

    private void Push()
    {
        _writeOutput(Output);
        OnPropertyChanged(nameof(Output));
    }
}
