using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Model;

namespace ViewModel;

public partial class CueListPanelViewModel(ShowService showService, FixturesListPanelViewModel fixtures) : ViewModelBase
{
    public ObservableCollection<CueViewModel> Cues { get; } = new(
        showService.CueList.Cues.Select(c => new CueViewModel(c))
    );

    // The highlighted row / "next cue to fire". Selection only — does not touch the stage.
    [ObservableProperty]
    public partial CueViewModel? SelectedCue { get; set; }

    // The cue currently live on stage (the last one GO'd). Tracked by reference, separate from selection.
    [ObservableProperty]
    public partial CueViewModel? ActiveCue { get; set; }

    private readonly CrossfadeEngine _crossfade = new();

    [RelayCommand]
    private void Go()
    {
        var target = SelectedCue ?? Cues.FirstOrDefault();
        if (target is null) return;

        Recall(target);

        // Advance selection to the next cue; at the end, keep it on the fired cue.
        var index = Cues.IndexOf(target);
        SelectedCue = index + 1 < Cues.Count ? Cues[index + 1] : target;
    }

    [RelayCommand]
    private void Record()
    {
        var (major, minor) = NextCueNumber();
        var cue = new Cue { CueMajor = major, CueMinor = minor };

        foreach (var fixture in fixtures.Fixtures)
        {
            cue.Fixtures.Add(new CueFixtureSnapshot
            {
                FixtureId = fixture.Fixture.Id,
                CapabilityValues = fixture.Capabilities.Select(c => c.Capture()).ToList()
            });
        }

        InsertSorted(cue);
        SyncActiveIndex();
    }

    [RelayCommand]
    private void Delete()
    {
        if (SelectedCue is null) return;

        var index = Cues.IndexOf(SelectedCue);
        if (SelectedCue == ActiveCue) ActiveCue = null;

        showService.CueList.Cues.RemoveAt(index);
        Cues.RemoveAt(index);

        // Keep a neighbour selected (the one that shifted into this slot, else the new last).
        SelectedCue = Cues.Count == 0 ? null : Cues[Math.Min(index, Cues.Count - 1)];
        SyncActiveIndex();
    }

    [RelayCommand]
    private void MoveUp() => Move(-1);

    [RelayCommand]
    private void MoveDown() => Move(1);

    private void Move(int delta)
    {
        if (SelectedCue is null) return;

        var from = Cues.IndexOf(SelectedCue);
        var to = from + delta;
        if (from < 0 || to < 0 || to >= Cues.Count) return;

        var moved = Cues[from];
        var neighbour = Cues[to];

        // Reordering renumbers: the two adjacent cues swap numbers, so the list stays
        // in numeric order while the selected cue changes slot (and keeps its content).
        (moved.Model.CueMajor, neighbour.Model.CueMajor) = (neighbour.Model.CueMajor, moved.Model.CueMajor);
        (moved.Model.CueMinor, neighbour.Model.CueMinor) = (neighbour.Model.CueMinor, moved.Model.CueMinor);
        moved.NotifyNumberChanged();
        neighbour.NotifyNumberChanged();

        Cues.Move(from, to);

        var model = showService.CueList.Cues[from];
        showService.CueList.Cues.RemoveAt(from);
        showService.CueList.Cues.Insert(to, model);

        // Keep the moved cue selected so the button can be pressed repeatedly.
        SelectedCue = moved;
        SyncActiveIndex();
    }

    // Inserts a cue into both the model list and the VM collection at its numeric
    // position, keeping the two index-aligned and the list sorted. Selects the new cue.
    private void InsertSorted(Cue cue)
    {
        var index = 0;
        while (index < showService.CueList.Cues.Count &&
               CompareNumber(showService.CueList.Cues[index], cue) < 0)
            index++;

        showService.CueList.Cues.Insert(index, cue);
        Cues.Insert(index, new CueViewModel(cue));
        SelectedCue = Cues[index];
    }

    private static int CompareNumber(Cue x, Cue y) =>
        x.CueMajor != y.CueMajor ? x.CueMajor.CompareTo(y.CueMajor) : x.CueMinor.CompareTo(y.CueMinor);

    private void Recall(CueViewModel cueVm)
    {
        var targets = new List<(CapabilityViewModelBase Capability, byte[] Target)>();

        foreach (var snapshot in cueVm.Model.Fixtures)
        {
            var fixtureVm = fixtures.Fixtures.FirstOrDefault(f => f.Fixture.Id == snapshot.FixtureId);
            if (fixtureVm is null) continue;

            foreach (var (capability, values) in fixtureVm.Capabilities.Zip(snapshot.CapabilityValues))
                targets.Add((capability, values));
        }

        // Crossfade from the current live state to the cue over its fade time (0 = hard cut).
        _crossfade.Start(targets, cueVm.Model.FadeIn);

        foreach (var vm in Cues) vm.IsActive = false;
        cueVm.IsActive = true;
        ActiveCue = cueVm;
        SyncActiveIndex();
    }

    // Keeps the persisted ActiveIndex in step with the active cue after any structural change.
    private void SyncActiveIndex() =>
        showService.CueList.ActiveIndex = ActiveCue is null ? -1 : Cues.IndexOf(ActiveCue);

    private (int Major, int Minor) NextCueNumber()
    {
        var cues = showService.CueList.Cues;

        // No selection, or the last cue is selected → append a fresh whole-number cue.
        if (SelectedCue is null || Cues.IndexOf(SelectedCue) == Cues.Count - 1)
        {
            var nextMajor = cues.Count == 0 ? 1 : cues.Max(c => c.CueMajor) + 1;
            return (nextMajor, 0);
        }

        // Mid-list → insert below the selected cue as the next free minor of its major.
        var major = SelectedCue.Model.CueMajor;
        var minor = SelectedCue.Model.CueMinor + 1;
        while (cues.Any(c => c.CueMajor == major && c.CueMinor == minor))
            minor++;
        return (major, minor);
    }
}
