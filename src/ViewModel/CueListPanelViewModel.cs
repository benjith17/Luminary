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
    private readonly ChaseEngine _chase = new();

    // The cue whose chase is currently looping, so the inspector can highlight the live step only
    // when that cue's steps are the ones on screen.
    private CueViewModel? _chaseCue;

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
        _chase.ProgressChanged += OnChaseProgress;
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

    // Steps of the selected cue's chase, for the inspector's step editor. Empty for snapshot cues.
    public ObservableCollection<ChaseStepViewModel> SelectedCueSteps { get; } = [];

    // Step selected in that list — the target of Update, Add Step and Delete Step.
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteStepCommand))]
    public partial ChaseStepViewModel? SelectedStep { get; set; }

    // The playback strip reuses one bar for two phases: the crossfade while it runs, then the
    // auto-follow countdown while one is pending. FadeProgress mirrors the engine; FollowProgress
    // is driven by _followClock; IsFollowPending recolours the bar and gives the follow priority.
    [ObservableProperty]
    public partial double FadeProgress { get; set; }

    [ObservableProperty]
    public partial double FollowProgress { get; set; }

    [ObservableProperty]
    public partial bool IsFollowPending { get; set; }

    // Progress through the running chase's current step, and whether one is running at all.
    [ObservableProperty]
    public partial double ChaseProgress { get; set; }

    [ObservableProperty]
    public partial bool IsChaseRunning { get; set; }

    // What the bar shows: a pending auto-follow countdown wins, then a running chase, then the fade.
    public double ProgressValue =>
        IsFollowPending ? FollowProgress : IsChaseRunning ? ChaseProgress : FadeProgress;

    // "follow e / t s" while counting down to an auto-follow, "step n / total" while a chase runs,
    // "e / t s" while fading, else the selected cue's fade time.
    public string ProgressReadout =>
        IsFollowPending
            ? $"follow {_followClock.Elapsed.TotalSeconds:0.0} / {_followDuration.TotalSeconds:0.0}s"
            : _chase.IsRunning
                ? $"step {_chase.StepIndex + 1} / {_chase.StepCount}"
                : _crossfade.IsFading
                    ? $"{_crossfade.Elapsed.TotalSeconds:0.0} / {_crossfade.Duration.TotalSeconds:0.0}s"
                    : SelectedCue?.FadeDisplay ?? "—";

    partial void OnFadeProgressChanged(double value) => NotifyProgress();
    partial void OnFollowProgressChanged(double value) => NotifyProgress();
    partial void OnIsFollowPendingChanged(bool value) => NotifyProgress();
    partial void OnChaseProgressChanged(double value) => NotifyProgress();
    partial void OnIsChaseRunningChanged(bool value) => NotifyProgress();

    private void NotifyProgress()
    {
        OnPropertyChanged(nameof(ProgressValue));
        OnPropertyChanged(nameof(ProgressReadout));
    }

    private void OnCrossfadeProgress() => FadeProgress = _crossfade.Progress;

    private void OnChaseProgress()
    {
        ChaseProgress = _chase.StepProgress;
        IsChaseRunning = _chase.IsRunning;
        RefreshCurrentStep();
    }

    partial void OnSelectedCueChanged(CueViewModel? value)
    {
        RefreshSelectedContents();
        OnPropertyChanged(nameof(ProgressReadout));
    }

    // Stops every playback timer this panel owns. Called when the show is swapped, so a chase
    // (which otherwise loops forever) can't keep driving the old show's fixtures.
    public void StopPlayback()
    {
        _chase.Stop();
        _crossfade.Stop();
        CancelFollow();
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
        var cue = new Cue { CueMajor = major, CueMinor = minor, Fixtures = CaptureStage() };
        InsertSorted(cue);
        SyncActiveIndex();
    }

    // Record a new chase cue seeded with the current stage as its first step. Further steps are
    // added from the inspector, so the workflow is: build a look, Chase, build the next look, + Step.
    [RelayCommand]
    private void RecordChase()
    {
        var (major, minor) = NextCueNumber();
        var cue = new Cue
        {
            CueMajor = major,
            CueMinor = minor,
            Type = CueType.Chase,
            Chase = new Chase { Steps = [new ChaseStep { Fixtures = CaptureStage() }] }
        };
        InsertSorted(cue);
        SyncActiveIndex();
    }

    // Re-record the current stage into the selection, keeping numbers, labels, fades and notes.
    // On a chase cue this replaces the selected step rather than the whole cue.
    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Update()
    {
        if (SelectedCue is null) return;

        if (SelectedCue.IsChase)
        {
            if (SelectedStep is null) return;
            SelectedStep.Model.Fixtures = CaptureStage();
            SelectedStep.NotifyContentsChanged();
            RestartIfLive(SelectedCue);
            return;
        }

        SelectedCue.Model.Fixtures = CaptureStage();
        SelectedCue.NotifyContentsChanged();
        RefreshSelectedContents();
    }

    private bool HasSelection() => SelectedCue is not null;

    // Append a step to the selected chase, captured from the current stage, and select it.
    [RelayCommand]
    private void AddStep()
    {
        if (SelectedCue is not { IsChase: true } cue) return;

        var step = new ChaseStep { Fixtures = CaptureStage() };
        cue.Model.Chase.Steps.Add(step);
        SelectedCueSteps.Add(new ChaseStepViewModel(step, SelectedCueSteps.Count));
        SelectedStep = SelectedCueSteps[^1];
        RestartIfLive(cue);
    }

    [RelayCommand(CanExecute = nameof(HasStepSelection))]
    private void DeleteStep()
    {
        if (SelectedCue is not { IsChase: true } cue || SelectedStep is null) return;

        var index = SelectedCueSteps.IndexOf(SelectedStep);
        cue.Model.Chase.Steps.RemoveAt(index);
        SelectedCueSteps.RemoveAt(index);
        Renumber();

        // Keep a neighbour selected (the one that shifted into this slot, else the new last).
        SelectedStep = SelectedCueSteps.Count == 0
            ? null
            : SelectedCueSteps[Math.Min(index, SelectedCueSteps.Count - 1)];

        RestartIfLive(cue);
    }

    private bool HasStepSelection() => SelectedStep is not null;

    // The engine works from its own resolved copy of the steps, so an edit to a chase that is
    // currently looping is invisible until it is re-fired — and a chase loops forever, so that
    // would mean never. Restarting on each deliberate edit (add / remove / re-record) keeps what
    // you see matching what you just did. Step duration, typed character by character, is left to
    // be picked up by the next restart.
    private void RestartIfLive(CueViewModel cue)
    {
        if (ReferenceEquals(_chaseCue, cue)) StartChase(cue);
    }

    private void Renumber()
    {
        for (var i = 0; i < SelectedCueSteps.Count; i++) SelectedCueSteps[i].Number = i + 1;
    }

    // Captures every fixture's current output as a fresh snapshot list.
    private List<CueFixtureSnapshot> CaptureStage() =>
        fixtures.Fixtures.Select(fixture => new CueFixtureSnapshot
        {
            FixtureId = fixture.Fixture.Id,
            CapabilityValues = fixture.Capabilities.Select(c => c.Capture()).ToList()
        }).ToList();

    [RelayCommand]
    private void Delete()
    {
        if (SelectedCue is null) return;

        var index = Cues.IndexOf(SelectedCue);
        if (SelectedCue == ActiveCue) ActiveCue = null;

        // Deleting the cue a chase is looping would leave it running with no cue behind it.
        if (ReferenceEquals(_chaseCue, SelectedCue))
        {
            _chase.Stop();
            _chaseCue = null;
        }

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
        // Any GO ends a running chase. It stops where it stands rather than releasing, so the
        // incoming cue crossfades out of the frozen look instead of snapping — both engines take
        // their start point from the live output.
        _chase.Stop();
        _chaseCue = null;

        if (cueVm.IsChase)
        {
            // A chase supplies its own per-step fades, so cancel any crossfade still in flight
            // rather than letting the two engines drive the same parameters.
            _crossfade.Stop();
            StartChase(cueVm);
        }
        else
        {
            // Crossfade from the current live state to the cue over its fade time (0 = hard cut).
            _crossfade.Start(ResolveTargets(cueVm.Model.Fixtures), cueVm.Model.FadeIn);
        }

        foreach (var vm in Cues) vm.IsActive = false;
        cueVm.IsActive = true;
        ActiveCue = cueVm;
        SyncActiveIndex();

        ArmFollow(cueVm);
    }

    // Resolves a stored snapshot list to the live capability VMs it drives, skipping fixtures that
    // are no longer in the show.
    private List<(CapabilityViewModelBase Capability, byte[] Target)> ResolveTargets(
        IEnumerable<CueFixtureSnapshot> snapshots)
    {
        var targets = new List<(CapabilityViewModelBase Capability, byte[] Target)>();

        foreach (var snapshot in snapshots)
        {
            var fixtureVm = fixtures.Fixtures.FirstOrDefault(f => f.Fixture.Id == snapshot.FixtureId);
            if (fixtureVm is null) continue;

            foreach (var (capability, values) in fixtureVm.Capabilities.Zip(snapshot.CapabilityValues))
                targets.Add((capability, values));
        }

        return targets;
    }

    // Resolves and launches a chase cue's steps. Steps that resolve to nothing (every fixture gone)
    // are dropped so they can't stall the loop on a step that drives no output.
    private void StartChase(CueViewModel cueVm)
    {
        var steps = cueVm.Model.Chase.Steps
            .Select(step => new ChaseStepTargets(ResolveTargets(step.Fixtures), step.Duration))
            .Where(step => step.Targets.Count > 0)
            .ToList();

        if (steps.Count == 0) return; // an empty chase simply holds the previous look

        _chaseCue = cueVm;
        _chase.Start(steps, cueVm.Model.FadeIn, cueVm.Model.Chase.StepFade);
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

    // Rebuilds the inspector for the selected cue: the Contents list for a snapshot cue, the step
    // list for a chase. Each snapshot resolves to its fixture number and name.
    private void RefreshSelectedContents()
    {
        SelectedCueContents.Clear();
        SelectedCueSteps.Clear();
        SelectedStep = null;

        if (SelectedCue is null) return;

        if (SelectedCue.IsChase)
        {
            foreach (var step in SelectedCue.Model.Chase.Steps)
                SelectedCueSteps.Add(new ChaseStepViewModel(step, SelectedCueSteps.Count));

            SelectedStep = SelectedCueSteps.FirstOrDefault();
            RefreshCurrentStep();
            return;
        }

        foreach (var snapshot in SelectedCue.Model.Fixtures)
        {
            var fixture = fixtures.Fixtures.FirstOrDefault(f => f.Fixture.Id == snapshot.FixtureId);
            SelectedCueContents.Add(fixture is null ? "· missing fixture" : $"{fixture.Number} · {fixture.Name}");
        }
    }

    // Marks the step the chase is currently on, but only while the cue being inspected is the one
    // actually running — otherwise the highlight would point at an unrelated cue's step list.
    private void RefreshCurrentStep()
    {
        var live = _chase.IsRunning && ReferenceEquals(_chaseCue, SelectedCue) ? _chase.StepIndex : -1;

        for (var i = 0; i < SelectedCueSteps.Count; i++)
            SelectedCueSteps[i].IsCurrent = i == live;
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
