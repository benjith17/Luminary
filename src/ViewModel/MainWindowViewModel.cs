using System.ComponentModel;
using ArtNet;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using Model;
using Persistence;

namespace ViewModel;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly FixtureLibrary _library = new();
    private readonly IShowStore _store = new JsonShowStore();

    private ShowService _show = null!;
    private ArtNetService? _artNet;

    // Serialized show as of the last save/load; compared against the current show to detect edits.
    private string _savedSnapshot = string.Empty;

    public EffectsPanelViewModel EffectsPanel { get; } = new();
    public FixtureEditorViewModel FixtureEditor { get; } = new();

    // Rebuilt whenever the show is swapped (New / Open), so the panels rebind to the new show.
    [ObservableProperty]
    public partial FixturesListPanelViewModel? FixturesListPanel { get; set; }

    [ObservableProperty]
    public partial CueListPanelViewModel? CueListPanel { get; set; }

    [ObservableProperty]
    public partial MacrosWindowViewModel? MacrosPanel { get; set; }

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
            lifetime.Exit += (_, _) => _artNet?.Dispose();

        LoadShow(CreateEmptyShow(), path: null);
    }

    public void New() => LoadShow(CreateEmptyShow(), path: null);

    public void Open(string path) => LoadShow(_store.Load(path, _library), path);

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
        _show = show;

        FixturesListPanel = new FixturesListPanelViewModel(show, _library);
        FixturesListPanel.PropertyChanged += OnFixtureSelectionChanged;
        FixturesListPanel.SelectedFixture = FixturesListPanel.Fixtures.FirstOrDefault();

        CueListPanel = new CueListPanelViewModel(show, FixturesListPanel);
        // Blackout is routed through this VM's property so macro-driven changes keep the toggle in sync.
        MacroHost = new MacroHost(FixturesListPanel, CueListPanel,
            getBlackout: () => Blackout, setBlackout: v => Blackout = v);
        MacrosPanel = new MacrosWindowViewModel(show, MacroHost);
        FixtureEditor.SelectedFixture = FixturesListPanel.SelectedFixture;

        _artNet = new ArtNetService(show);
        CurrentPath = path;
        _savedSnapshot = _store.Serialize(show);
        OnPropertyChanged(nameof(Blackout));
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
