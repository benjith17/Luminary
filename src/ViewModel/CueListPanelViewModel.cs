using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Model;

namespace ViewModel;

public partial class CueListPanelViewModel : ViewModelBase
{
    private readonly ShowService showService;
    private readonly FixturesListPanelViewModel fixtures;
    private readonly CrossfadeEngine _crossfade = new();

    // Repeating timer driving the auto-follow countdown; when the clock reaches the follow duration
    // it fires _followTarget. Kept as its own clock so the strip can show a live countdown.
    private readonly DispatcherTimer _followTimer;
    private readonly Stopwatch _followClock = new();
    private TimeSpan _followDuration;
    private CueViewModel? _followTarget;

    public CueListPanelViewModel(ShowService showService, FixturesListPanelViewModel fixtures)
    {
        this.showService = showService;
        this.fixtures = fixtures;

        Cues = new ObservableCollection<CueViewModel>(
            showService.CueList.Cues.Select(c => new CueViewModel(c)));

        _crossfade.ProgressChanged += OnCrossfadeProgress;
        _followTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(25) }; // ~40fps
        _followTimer.Tick += OnFollowTick;
    }

    public ObservableCollection<CueViewModel> Cues { get; }

    // The highlighted row / "next cue to fire". Selection only — does not touch the stage.
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UpdateCommand))]
    public partial CueViewModel? SelectedCue { get; set; }

    // The cue currently live on stage (the last one GO'd). Tracked by reference, separate from selection.
    [ObservableProperty]
    public partial CueViewModel? ActiveCue { get; set; }

    // View state only: whether the per-cue inspector sidebar is shown. Not persisted.
    [ObservableProperty]
    public partial bool ShowInspector { get; set; } = false;

    // Fixtures captured in the selected cue, for the inspector's Contents list ("<number> · <name>").
    public ObservableCollection<string> SelectedCueContents { get; } = [];

    // The playback strip reuses one bar for two phases: the crossfade while it runs, then the
    // auto-follow countdown while one is pending. FadeProgress mirrors the engine; FollowProgress
    // is driven by _followClock; IsFollowPending recolours the bar and gives the follow priority.
    [ObservableProperty]
    public partial double FadeProgress { get; set; }

    [ObservableProperty]
    public partial double FollowProgress { get; set; }

    [ObservableProperty]
    public partial bool IsFollowPending { get; set; }

    // What the bar shows: a pending auto-follow countdown takes precedence over the fade.
    public double ProgressValue => IsFollowPending ? FollowProgress : FadeProgress;

    // "follow e / t s" while counting down to an auto-follow, "e / t s" while fading, else the
    // selected cue's fade time.
    public string ProgressReadout =>
        IsFollowPending
            ? $"follow {_followClock.Elapsed.TotalSeconds:0.0} / {_followDuration.TotalSeconds:0.0}s"
            : _crossfade.IsFading
                ? $"{_crossfade.Elapsed.TotalSeconds:0.0} / {_crossfade.Duration.TotalSeconds:0.0}s"
                : SelectedCue?.FadeDisplay ?? "—";

    partial void OnFadeProgressChanged(double value) => NotifyProgress();
    partial void OnFollowProgressChanged(double value) => NotifyProgress();
    partial void OnIsFollowPendingChanged(bool value) => NotifyProgress();

    private void NotifyProgress()
    {
        OnPropertyChanged(nameof(ProgressValue));
        OnPropertyChanged(nameof(ProgressReadout));
    }

    private void OnCrossfadeProgress() => FadeProgress = _crossfade.Progress;

    partial void OnSelectedCueChanged(CueViewModel? value)
    {
        RefreshSelectedContents();
        OnPropertyChanged(nameof(ProgressReadout));
    }

    [RelayCommand]
    private void Go() => GoSelected();

    // Fire the selected cue and advance. Returns false if there was nothing to fire (used by macros
    // to report a "GO with no cue" problem). Safe to call from the macro interpreter (UI thread).
    public bool GoSelected()
    {
        var target = SelectedCue ?? Cues.FirstOrDefault();
        if (target is null) return false;
        FireAndAdvance(target);
        return true;
    }

    // Jump to a cue by number and fire it. Minor == null selects the lowest-minor cue of that major.
    // Returns false if no such cue exists.
    public bool GoTo(int major, int? minor)
    {
        var target = minor is { } m
            ? Cues.FirstOrDefault(c => c.Model.CueMajor == major && c.Model.CueMinor == m)
            : Cues.Where(c => c.Model.CueMajor == major)
                  .OrderBy(c => c.Model.CueMinor)
                  .FirstOrDefault();

        if (target is null) return false;
        FireAndAdvance(target);
        return true;
    }

    // Move the selection up/down without firing (for keyboard cue navigation). Clamps at the ends.
    public void SelectPrevious()
    {
        if (Cues.Count == 0) return;
        var index = SelectedCue is null ? 0 : Cues.IndexOf(SelectedCue) - 1;
        SelectedCue = Cues[Math.Max(0, index)];
    }

    public void SelectNext()
    {
        if (Cues.Count == 0) return;
        var index = SelectedCue is null ? 0 : Cues.IndexOf(SelectedCue) + 1;
        SelectedCue = Cues[Math.Min(Cues.Count - 1, index)];
    }

    private void FireAndAdvance(CueViewModel target)
    {
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
        CaptureInto(cue);
        InsertSorted(cue);
        SyncActiveIndex();
    }

    // Re-record the current stage into the selected cue, keeping its number, label, fade and notes.
    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Update()
    {
        if (SelectedCue is null) return;
        CaptureInto(SelectedCue.Model);
        SelectedCue.NotifyContentsChanged();
        RefreshSelectedContents();
    }

    private bool HasSelection() => SelectedCue is not null;

    // Captures every fixture's current output into the cue, replacing any prior snapshots.
    private void CaptureInto(Cue cue)
    {
        cue.Fixtures.Clear();
        foreach (var fixture in fixtures.Fixtures)
        {
            cue.Fixtures.Add(new CueFixtureSnapshot
            {
                FixtureId = fixture.Fixture.Id,
                CapabilityValues = fixture.Capabilities.Select(c => c.Capture()).ToList()
            });
        }
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

        ArmFollow(cueVm);
    }

    // ---- Auto-follow --------------------------------------------------------------------------

    // After a cue fires, schedule the next cue to fire automatically if this one has a follow time.
    // Any fire (manual or auto) re-arms this, so a manual GO cleanly interrupts and restarts a chain.
    private void ArmFollow(CueViewModel firedCue)
    {
        CancelFollow();

        if (firedCue.Model.Follow is not { } delay || delay <= TimeSpan.Zero) return;

        var index = Cues.IndexOf(firedCue);
        if (index < 0 || index + 1 >= Cues.Count) return; // last cue: nothing to follow into

        _followTarget = Cues[index + 1];
        _followDuration = delay;
        _followClock.Restart();
        FollowProgress = 0;
        IsFollowPending = true;
        _followTimer.Start();
    }

    private void CancelFollow()
    {
        _followTimer.Stop();
        _followClock.Reset();
        _followTarget = null;
        FollowProgress = 0;
        IsFollowPending = false;
    }

    private void OnFollowTick(object? sender, EventArgs e)
    {
        if (_followTarget is null) { CancelFollow(); return; }

        var t = _followClock.Elapsed / _followDuration;
        if (t < 1.0)
        {
            FollowProgress = t; // drives the strip's countdown bar + readout
            return;
        }

        // Time's up: fire the captured target if it's still present (the list may have changed).
        var target = _followTarget;
        CancelFollow();
        if (Cues.Contains(target)) FireAndAdvance(target);
    }

    // Rebuilds the inspector's Contents list for the selected cue, resolving each snapshot to its
    // fixture number and name.
    private void RefreshSelectedContents()
    {
        SelectedCueContents.Clear();
        if (SelectedCue is null) return;

        foreach (var snapshot in SelectedCue.Model.Fixtures)
        {
            var fixture = fixtures.Fixtures.FirstOrDefault(f => f.Fixture.Id == snapshot.FixtureId);
            SelectedCueContents.Add(fixture is null ? "· missing fixture" : $"{fixture.Number} · {fixture.Name}");
        }
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
