using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Model;

namespace ViewModel;

// The Keyframes tab. Cues are created in the cue list and edited here: pick a keyframed cue, add a
// track per capability you want to automate, park the playhead and record what the stage looks
// like at that instant.
//
// Values are never drawn by hand — a key is recorded from the live rig, the same gesture that
// records a cue. That keeps a key capability-shaped (a colour key is a colour, not three curves)
// and means the whole existing programming surface doubles as the value editor.
public partial class KeyframeEditorViewModel : ViewModelBase
{
    private readonly FixturesListPanelViewModel _fixtures;
    private readonly CueListPanelViewModel _cues;

    // Beat divisions offered for snapping.
    public GridDivision[] Divisions { get; } =
    [
        new("Bar", 4), new("1/2 bar", 2), new("Beat", 1), new("1/2", 0.5), new("1/4", 0.25)
    ];

    public KeyframeEditorViewModel(FixturesListPanelViewModel fixtures, CueListPanelViewModel cues)
    {
        _fixtures = fixtures;
        _cues = cues;

        SelectedDivision = Divisions[2]; // beat
        SelectedFixtureForAdd = fixtures.Fixtures.FirstOrDefault();

        _cues.Keyframes.ProgressChanged += OnEngineProgress;
        _cues.Cues.CollectionChanged += OnCuesChanged;
        RefreshCueList();
    }

    // ---- Cue selection --------------------------------------------------------------------

    // The keyframed cues in the show, mirrored from the cue list so recording one here shows up
    // immediately.
    public ObservableCollection<CueViewModel> KeyCues { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCue))]
    public partial CueViewModel? SelectedCue { get; set; }

    public bool HasCue => SelectedCue is not null;

    public ObservableCollection<KeyframeTrackViewModel> Tracks { get; } = [];

    partial void OnSelectedCueChanged(CueViewModel? value)
    {
        RebuildTracks();

        if (value is null)
        {
            _cues.Keyframes.Stop();
            return;
        }

        _cues.LoadKeyframesForEditing(value);
        NotifyTransport();
        NotifySequence();
    }

    private void OnCuesChanged(object? sender, NotifyCollectionChangedEventArgs e) => RefreshCueList();

    private void RefreshCueList()
    {
        var keyed = _cues.Cues.Where(c => c.IsKeys).ToList();

        // Rebuild only when the set actually differs, so selection survives unrelated cue edits.
        if (keyed.Count == KeyCues.Count && !keyed.Where((c, i) => c != KeyCues[i]).Any()) return;

        var previous = SelectedCue;
        KeyCues.Clear();
        foreach (var cue in keyed) KeyCues.Add(cue);

        SelectedCue = previous is not null && KeyCues.Contains(previous) ? previous : KeyCues.FirstOrDefault();
    }

    // Called by the window when the cue list asks to edit a cue, so the tab opens on it.
    public void Edit(CueViewModel cue)
    {
        RefreshCueList();
        if (KeyCues.Contains(cue)) SelectedCue = cue;
    }

    // ---- Tracks ---------------------------------------------------------------------------

    public ObservableCollection<FixtureListItemViewModel> AvailableFixtures => _fixtures.Fixtures;

    [ObservableProperty]
    public partial FixtureListItemViewModel? SelectedFixtureForAdd { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveTrackCommand))]
    [NotifyCanExecuteChangedFor(nameof(RecordTrackKeyCommand))]
    public partial KeyframeTrackViewModel? SelectedTrack { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteKeyCommand))]
    [NotifyPropertyChangedFor(nameof(SelectedKeyInterpolation))]
    [NotifyPropertyChangedFor(nameof(HasSelectedKey))]
    public partial KeyframeViewModel? SelectedKey { get; set; }

    public bool HasSelectedKey => SelectedKey is not null;

    private void RebuildTracks()
    {
        Tracks.Clear();
        SelectedTrack = null;
        SelectedKey = null;

        if (SelectedCue is null) return;

        foreach (var track in SelectedCue.Model.Keys.Tracks)
        {
            var fixture = _fixtures.Fixtures.FirstOrDefault(f => f.Fixture.Id == track.FixtureId);
            if (fixture is null) continue;
            if (track.CapabilityIndex < 0 || track.CapabilityIndex >= fixture.Capabilities.Count) continue;

            Tracks.Add(new KeyframeTrackViewModel(
                track, fixture, fixture.Capabilities[track.CapabilityIndex], this));
        }

        SelectedTrack = Tracks.FirstOrDefault();
    }

    // Adds a track for every capability of the chosen fixture that isn't automated yet.
    [RelayCommand]
    private void AddTracks()
    {
        if (SelectedCue is null || SelectedFixtureForAdd is not { } fixture) return;

        var sequence = SelectedCue.Model.Keys;
        var added = false;

        for (var i = 0; i < fixture.Capabilities.Count; i++)
        {
            if (sequence.Tracks.Any(t => t.FixtureId == fixture.Fixture.Id && t.CapabilityIndex == i)) continue;

            var track = new KeyframeTrack { FixtureId = fixture.Fixture.Id, CapabilityIndex = i };
            sequence.Tracks.Add(track);
            Tracks.Add(new KeyframeTrackViewModel(track, fixture, fixture.Capabilities[i], this));
            added = true;
        }

        if (!added) return;

        SelectedTrack ??= Tracks.FirstOrDefault();
        OnTrackSetChanged();
    }

    [RelayCommand(CanExecute = nameof(HasTrack))]
    private void RemoveTrack()
    {
        if (SelectedCue is null || SelectedTrack is not { } track) return;

        var index = Tracks.IndexOf(track);
        SelectedCue.Model.Keys.Tracks.Remove(track.Model);
        Tracks.Remove(track);

        if (SelectedKey is not null && track.Keys.Contains(SelectedKey)) SelectedKey = null;
        SelectedTrack = Tracks.Count == 0 ? null : Tracks[Math.Min(index, Tracks.Count - 1)];
        OnTrackSetChanged();
    }

    private bool HasTrack() => SelectedTrack is not null;

    // The engine resolves tracks once, so adding or removing one needs a reload. Individual keys
    // are held by reference and need nothing.
    private void OnTrackSetChanged()
    {
        _cues.ReloadKeyframeTracks();
        SelectedCue?.NotifyKeysChanged();
        NotifySequence();
    }

    // ---- Keys -----------------------------------------------------------------------------

    // Records the whole stage into every track at the playhead — the usual gesture: build a look,
    // park the playhead, key it.
    [RelayCommand]
    private void RecordKey()
    {
        if (SelectedCue is null || Tracks.Count == 0) return;

        var at = SnapTime(Playhead);
        foreach (var track in Tracks) track.SetKeyAt(at);

        AfterKeyEdit();
    }

    // Records only the selected track, for tightening one element without touching the rest.
    [RelayCommand(CanExecute = nameof(HasTrack))]
    private void RecordTrackKey()
    {
        if (SelectedTrack is not { } track) return;

        SelectedKey = track.SetKeyAt(SnapTime(Playhead));
        AfterKeyEdit();
    }

    [RelayCommand(CanExecute = nameof(HasSelectedKey))]
    private void DeleteKey()
    {
        if (SelectedKey is not { } key) return;

        var owner = Tracks.FirstOrDefault(t => t.Keys.Contains(key));
        owner?.Remove(key);
        SelectedKey = null;
        AfterKeyEdit();
    }

    public Interpolation[] InterpolationOptions { get; } = Enum.GetValues<Interpolation>();

    public Interpolation SelectedKeyInterpolation
    {
        get => SelectedKey?.Interpolation ?? Interpolation.Linear;
        set
        {
            if (SelectedKey is not { } key || key.Interpolation == value) return;
            key.Interpolation = value;
            OnPropertyChanged();
            Tracks.FirstOrDefault(t => t.Keys.Contains(key))?.NotifyChanged();
            RefreshPreview();
        }
    }

    // Selects a row without disturbing the key selection, as long as that key lives on this row —
    // a key belonging to some other track would leave the two selections describing different rows.
    public void SelectTrack(KeyframeTrackViewModel track) =>
        Select(track, SelectedKey is { } key && track.Keys.Contains(key) ? key : null);

    // Records a key on one track at the playhead and selects it.
    public void AddKeyTo(KeyframeTrackViewModel track)
    {
        var key = track.SetKeyAt(SnapTime(Playhead));
        Select(track, key);
        AfterKeyEdit();
    }

    // Called by a lane when the operator clicks a key (or empty space, to clear the selection).
    public void Select(KeyframeTrackViewModel track, KeyframeViewModel? key)
    {
        foreach (var t in Tracks) t.IsSelected = t == track;
        SelectedTrack = track;

        foreach (var k in Tracks.SelectMany(t => t.Keys)) k.IsSelected = k == key;
        SelectedKey = key;

        // Selection is drawn by the lanes, which redraw on Revision, so every row needs poking —
        // the row losing the selection as much as the one gaining it.
        foreach (var t in Tracks) t.NotifyChanged();
    }

    // A key moved by dragging: re-apply the current instant so the rig follows the edit.
    public void NotifyKeyDragged() => AfterKeyEdit();

    private void AfterKeyEdit()
    {
        SelectedCue?.NotifyKeysChanged();
        RefreshPreview();
    }

    // ---- Transport ------------------------------------------------------------------------

    private KeyframeEngine Engine => _cues.Keyframes;

    public TimeSpan Playhead => Engine.CurrentTime;

    public double PlayheadSeconds => Playhead.TotalSeconds;

    // 0..1 across the timeline, which is what the playhead overlay is positioned by.
    public double PlayheadFraction => DurationSeconds <= 0 ? 0 : PlayheadSeconds / DurationSeconds;

    public bool IsPlaying => Engine.IsRunning;

    public double DurationSeconds => SelectedCue?.KeysDurationSeconds ?? 10;

    public string TimeReadout => $"{PlayheadSeconds:0.00}s · {BarBeat(PlayheadSeconds)}";

    [RelayCommand]
    private void PlayPause()
    {
        if (SelectedCue is null) return;

        if (Engine.IsRunning) Engine.Pause();
        else
        {
            // Restart from the top when parked at the end, so Play is never a no-op.
            if (Engine.CurrentTime >= Engine.Duration) Engine.Seek(TimeSpan.Zero);
            Engine.Resume();
        }

        NotifyTransport();
    }

    [RelayCommand]
    private void Rewind() => Seek(TimeSpan.Zero);

    // Seeks and applies that instant to the rig, so scrubbing shows the look under the playhead.
    public void Seek(TimeSpan time)
    {
        Engine.Seek(ClampToTimeline(time));
        NotifyTransport();
    }

    public void SeekToFraction(double fraction) =>
        Seek(TimeSpan.FromSeconds(SnapSeconds(fraction * DurationSeconds)));

    // Re-applies the current instant after an edit, without disturbing play state.
    private void RefreshPreview()
    {
        if (!Engine.IsRunning) Engine.Seek(Engine.CurrentTime);
    }

    private void OnEngineProgress() => NotifyTransport();

    private void NotifyTransport()
    {
        OnPropertyChanged(nameof(Playhead));
        OnPropertyChanged(nameof(PlayheadSeconds));
        OnPropertyChanged(nameof(PlayheadFraction));
        OnPropertyChanged(nameof(TimeReadout));
        OnPropertyChanged(nameof(IsPlaying));
        OnPropertyChanged(nameof(PlayLabel));
    }

    public string PlayLabel => IsPlaying ? "❚❚" : "▶";

    // ---- Sequence settings ----------------------------------------------------------------

    public double SequenceDuration
    {
        get => DurationSeconds;
        set
        {
            if (SelectedCue is not { } cue) return;
            cue.KeysDurationSeconds = value;
            _cues.ReloadKeyframeTracks();
            OnPropertyChanged();
            NotifySequence();
            NotifyTransport();
        }
    }

    public bool SequenceLoop
    {
        get => SelectedCue?.KeysLoop ?? false;
        set
        {
            if (SelectedCue is not { } cue) return;
            cue.KeysLoop = value;
            _cues.ReloadKeyframeTracks();
            OnPropertyChanged();
        }
    }

    private void NotifySequence()
    {
        OnPropertyChanged(nameof(SequenceDuration));
        OnPropertyChanged(nameof(SequenceLoop));
        OnPropertyChanged(nameof(DurationSeconds));
        OnPropertyChanged(nameof(GridSeconds));
        NotifyTimebase();
    }

    // ---- Beat grid ------------------------------------------------------------------------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GridSeconds))]
    public partial double Bpm { get; set; } = 120;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GridSeconds))]
    public partial GridDivision? SelectedDivision { get; set; }

    [ObservableProperty]
    public partial bool SnapEnabled { get; set; } = true;

    // Spacing of the grid, in seconds. Also what keys snap to.
    public double GridSeconds =>
        Bpm <= 0 || SelectedDivision is null ? 0 : 60.0 / Bpm * SelectedDivision.Beats;

    partial void OnBpmChanged(double value) => NotifyTimebase();
    partial void OnSelectedDivisionChanged(GridDivision? value) => NotifyTimebase();

    // The lanes read their timebase through their own track view model, so a change to the grid or
    // the sequence length has to be announced on every row.
    private void NotifyTimebase()
    {
        foreach (var track in Tracks) track.NotifyTimebaseChanged();
    }

    public double SnapSeconds(double seconds)
    {
        var grid = GridSeconds;
        if (!SnapEnabled || grid <= 0) return Math.Max(0, seconds);

        return Math.Max(0, Math.Round(seconds / grid) * grid);
    }

    public TimeSpan SnapTime(TimeSpan time) => TimeSpan.FromSeconds(SnapSeconds(time.TotalSeconds));

    public TimeSpan ClampToTimeline(TimeSpan time)
    {
        var max = TimeSpan.FromSeconds(DurationSeconds);
        return time < TimeSpan.Zero ? TimeSpan.Zero : time > max ? max : time;
    }

    // Musical position, counting from bar 1 beat 1, in 4/4.
    private string BarBeat(double seconds)
    {
        if (Bpm <= 0) return "—";

        var beats = seconds / (60.0 / Bpm);
        return $"bar {(int)(beats / 4) + 1} beat {(int)(beats % 4) + 1}";
    }
}
