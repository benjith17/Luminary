using CommunityToolkit.Mvvm.ComponentModel;
using Model;

namespace ViewModel;

// One row of the chase step list in the cue inspector.
public partial class ChaseStepViewModel(ChaseStep step, int index) : ViewModelBase
{
    public ChaseStep Model { get; } = step;

    // 1-based position in the chase. Reassigned when steps are added, removed or reordered.
    [ObservableProperty]
    public partial int Number { get; set; } = index + 1;

    // How long the chase rests on this step, edited in seconds. Floored so a mistyped 0 can't
    // spin the chase at frame rate.
    public double DurationSeconds
    {
        get => Model.Duration.TotalSeconds;
        set
        {
            var seconds = Math.Max(0.05, value);
            if (SetProperty(Model.Duration.TotalSeconds, seconds, Model,
                    (m, v) => m.Duration = TimeSpan.FromSeconds(v)))
                OnPropertyChanged(nameof(DurationDisplay));
        }
    }

    public string DurationDisplay => $"{Model.Duration.TotalSeconds:0.##}s";

    public int FixtureCount => Model.Fixtures.Count;

    // Re-recording a step replaces its contents in place; refresh the count.
    public void NotifyContentsChanged() => OnPropertyChanged(nameof(FixtureCount));

    // True while the chase is live on this step, so the list can show where playback has reached.
    [ObservableProperty]
    public partial bool IsCurrent { get; set; }
}
