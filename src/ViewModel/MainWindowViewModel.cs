using ArtNet;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using Model;

namespace ViewModel;

public partial class MainWindowViewModel : ViewModelBase
{
    public EffectsPanelViewModel EffectsPanel { get; } = new();
    public FixturesListPanelViewModel FixturesListPanel { get; }
    public FixtureEditorViewModel FixtureEditor { get; } = new();
    public CueListPanelViewModel CueListPanel { get; }

    public MainWindowViewModel()
    {
        var library = new FixtureLibrary();
        var show = CreateTestShow(library);

        FixturesListPanel = new(show, library);
        FixturesListPanel.SelectedFixture = FixturesListPanel.Fixtures.FirstOrDefault();
        CueListPanel = new(show, FixturesListPanel);

        FixturesListPanel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(FixturesListPanelViewModel.SelectedFixture))
                FixtureEditor.SelectedFixture = FixturesListPanel.SelectedFixture;
        };

        FixtureEditor.SelectedFixture = FixturesListPanel.SelectedFixture;

        var artNetService = new ArtNetService(show);

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
            lifetime.Exit += (_, _) => artNetService.Dispose();
    }

    private static ShowService CreateTestShow(FixtureLibrary library)
    {
        var show = new ShowService
        {
            Universes = [new Universe(1)]
        };

        if (library.Get("Encore Strobe") is { } encore)
            show.Fixtures.Add(new Fixture("Encore Strobe", channel: 0, encore) { UniverseNumber = 1 });

        return show;
    }
}
