using System.IO;
using Fixtures;
using System.ComponentModel;
using ArtNet;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using Midi;
using Model;
using Persistence;

namespace ViewModel;

public partial class MainWindowViewModel : ViewModelBase
{
    private FixtureLibrary _library = new();
    private readonly IShowStore _store = new JsonShowStore();
    private readonly MidiInputService _midi = new();

    private ShowService _show = null!;
    private ArtNetService? _artNet;
    private MidiBindingDispatcher? _midiDispatcher;

    // Serialized show as of the last save/load; compared against the current show to detect edits.
    private string _savedSnapshot = string.Empty;

    public EffectsPanelViewModel EffectsPanel { get; } = new();
    public FixtureEditorViewModel FixtureEditor { get; } = new();

    // Live view of incoming MIDI (app-level, since the MIDI service spans all shows).
    public MidiMonitorViewModel MidiMonitor { get; }

    // Rebuilt whenever the show is swapped (New / Open), so the panels rebind to the new show.
    [ObservableProperty]
    public partial FixturesListPanelViewModel? FixturesListPanel { get; set; }

    [ObservableProperty]
    public partial CueListPanelViewModel? CueListPanel { get; set; }

    [ObservableProperty]
    public partial MacrosWindowViewModel? MacrosPanel { get; set; }

    [ObservableProperty]
    public partial BindingsWindowViewModel? BindingsPanel { get; set; }

    // The Keyframes tab, rebuilt with the rest of the show-bound panels.
    [ObservableProperty]
    public partial KeyframeEditorViewModel? KeyframeEditor { get; set; }

    // Which workspace tab is showing. Settable so the cue list can send the operator to the
    // Keyframes tab when they choose to edit a keyframed cue.
    [ObservableProperty]
    public partial int SelectedTab { get; set; }

    // Index of the Keyframes tab in MainWindow's TabControl.
    private const int KeyframesTab = 1;

    // App-level macro host driving the live show — shared by the Macros window and (later) input
    // bindings, so every input source runs macros against the same fixtures / cues / blackout.
    public MacroHost? MacroHost { get; private set; }

    // Path of the show file currently open (null for a new / never-saved show).
    public string? CurrentPath { get; private set; }

    // True when the show has changes that differ from the last saved/loaded state.
    public bool IsDirty => _store.Serialize(_show) != _savedSnapshot;

    // Live output kill: zeros all transmitted channels while active; releasing restores the look.
    public bool Blackout
    {
        get => _show.Blackout;
        set
        {
            if (_show.Blackout == value) return;
            _show.Blackout = value;
            OnPropertyChanged();
        }
    }

    public MainWindowViewModel()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
            lifetime.Exit += (_, _) =>
            {
                _artNet?.Dispose();
                _midi.Dispose();
            };

        MidiMonitor = new MidiMonitorViewModel(_midi);
        // Route every inbound message to the current show's dispatcher (rebuilt on each LoadShow).
        _midi.MessageReceived += m => _midiDispatcher?.Dispatch(m);

        _library = FixtureLibraryLoader.Load();
        LoadShow(CreateEmptyShow(), path: null);

        _ = _midi.StartAsync();
    }

    public void New()
    {
        _library = FixtureLibraryLoader.Load();
        LoadShow(CreateEmptyShow(), path: null);
    }

    public void Open(string path)
    {
        // Reload before reading the show: a show can travel with a .lumfl beside it, and that
        // library is only visible once we know which folder the show lives in.
        _library = FixtureLibraryLoader.Load(Path.GetDirectoryName(path));
        LoadShow(_store.Load(path, _library), path);
    }

    /// <summary>Problems found loading the fixture libraries — bad files, clashing packs.</summary>
    public IReadOnlyList<LoadIssue> LibraryIssues => _library.Issues;

    /// <summary>
    /// Patched fixtures whose personality this installation cannot resolve. They hold their place
    /// in the patch and emit no DMX, but the operator has to be told: the rig will be short.
    /// </summary>
    public IReadOnlyList<Fixture> MissingFixtures =>
        [.. _show.Fixtures.Where(f => f.FixtureType.IsMissing)];

    public void Save(string path)
    {
        _store.Save(_show, path);
        _savedSnapshot = _store.Serialize(_show);
        CurrentPath = path;
    }

    // Swaps the entire show: disposes the old output, rebuilds the show-bound panels, and
    // re-points Art-Net at the new universes.
    private void LoadShow(ShowService show, string? path)
    {
        _artNet?.Dispose();
        MacrosPanel?.StopAll(); // don't let the old show's panel-triggered macros run against the new one
        CueListPanel?.StopPlayback(); // a chase loops forever — stop it before the show is swapped out
        _show = show;

        FixturesListPanel = new FixturesListPanelViewModel(show, _library);
        FixturesListPanel.PropertyChanged += OnFixtureSelectionChanged;
        FixturesListPanel.SelectedFixture = FixturesListPanel.Fixtures.FirstOrDefault();

        CueListPanel = new CueListPanelViewModel(show, FixturesListPanel);
        KeyframeEditor = new KeyframeEditorViewModel(FixturesListPanel, CueListPanel);
        CueListPanel.EditKeyframesRequested += OnEditKeyframesRequested;
        // Blackout is routed through this VM's property so macro-driven changes keep the toggle in sync.
        MacroHost = new MacroHost(FixturesListPanel, CueListPanel,
            getBlackout: () => Blackout, setBlackout: v => Blackout = v);
        MacrosPanel = new MacrosWindowViewModel(show, MacroHost);
        // MIDI bindings are per-show, so rebuild the dispatcher against the new show's bindings.
        _midiDispatcher = new MidiBindingDispatcher(show.Bindings, MacroHost);
        BindingsPanel = new BindingsWindowViewModel(show, _midi, RebuildMidiBindings);
        FixtureEditor.SelectedFixture = FixturesListPanel.SelectedFixture;

        _artNet = new ArtNetService(show);
        CurrentPath = path;
        _savedSnapshot = _store.Serialize(show);
        OnPropertyChanged(nameof(Blackout));
    }

    // Rebuild the live MIDI dispatcher after the show's bindings are edited, so changes apply at once.
    public void RebuildMidiBindings()
    {
        if (MacroHost is not null)
            _midiDispatcher = new MidiBindingDispatcher(_show.Bindings, MacroHost);
    }

    // "Edit" on a keyframed cue jumps to the Keyframes tab with that cue open.
    private void OnEditKeyframesRequested(CueViewModel cue)
    {
        KeyframeEditor?.Edit(cue);
        SelectedTab = KeyframesTab;
    }

    private void OnFixtureSelectionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FixturesListPanelViewModel.SelectedFixture) &&
            sender is FixturesListPanelViewModel panel)
            FixtureEditor.SelectedFixture = panel.SelectedFixture;
    }

    private static ShowService CreateEmptyShow() => new()
    {
        Universes = [new Universe(1)]
    };
}
