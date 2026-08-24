using CommunityToolkit.Mvvm.ComponentModel;
using Model;

namespace ViewModel;

public partial class CueViewModel(Cue cue) : ViewModelBase
{
    public Cue Model { get; } = cue;

    public string DisplayNumber => Model.DisplayNumber;

    public string Label
    {
        get => Model.Label;
        set => SetProperty(Model.Label, value, Model, (c, v) => c.Label = v);
    }

    // Crossfade duration for this cue, edited in seconds. Kept non-negative.
    public double FadeSeconds
    {
        get => Model.FadeIn.TotalSeconds;
        set
        {
            var seconds = Math.Max(0, value);
            if (SetProperty(Model.FadeIn.TotalSeconds, seconds, Model, (m, v) => m.FadeIn = TimeSpan.FromSeconds(v)))
                OnPropertyChanged(nameof(FadeDisplay));
        }
    }

    public string FadeDisplay => $"{Model.FadeIn.TotalSeconds:0.0}s";

    // Operator note for this cue.
    public string Notes
    {
        get => Model.Notes;
        set => SetProperty(Model.Notes, value, Model, (c, v) => c.Notes = v);
    }

    // Auto-follow time as edited in the inspector. Blank / non-positive clears it (no follow).
    public string FollowText
    {
        get => Model.Follow is { } f ? f.TotalSeconds.ToString("0.###") : "";
        set
        {
            TimeSpan? parsed = double.TryParse(value, out var s) && s > 0 ? TimeSpan.FromSeconds(s) : null;
            if (SetProperty(Model.Follow, parsed, Model, (m, v) => m.Follow = v))
                OnPropertyChanged(nameof(FollowDisplay));
        }
    }

    // Compact follow readout for the table column ("—" when off).
    public string FollowDisplay => Model.Follow is { } f ? $"{f.TotalSeconds:0.#}s" : "—";

    // Number of fixtures captured in this cue (shown in the inspector's Contents).
    public int FixtureCount => Model.Fixtures.Count;

    // Re-recording a cue changes its contents in place; refresh the count.
    public void NotifyContentsChanged() => OnPropertyChanged(nameof(FixtureCount));

    // The cue currently live on stage (the last one GO'd). Distinct from list selection.
    [ObservableProperty]
    public partial bool IsActive { get; set; }

    // True while the row's label is being renamed inline.
    [ObservableProperty]
    public partial bool IsEditing { get; set; }

    // Reordering renumbers cues, so the displayed number can change after construction.
    public void NotifyNumberChanged() => OnPropertyChanged(nameof(DisplayNumber));
}
