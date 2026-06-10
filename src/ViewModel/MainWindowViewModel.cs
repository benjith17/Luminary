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
        var show = CreateTestShow();

        FixturesListPanel = new(show);
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

    private static ShowService CreateTestShow()
    {
        var universe = new Universe(1, targetIp: "127.0.0.1");

        var dimmerDef = new FixtureDefinition
        {
            Name = "Generic Dimmer",
            Capabilities = [new DimmerCapability("Dimmer", offset: 0)]
        };

        var rgbDef = new FixtureDefinition
        {
            Name = "Generic RGB",
            Capabilities = [new ColorCapability("Color", redOffset: 0, greenOffset: 1, blueOffset: 2)]
        };

        var bigTestLightDef = new FixtureDefinition
        {
            Name = "Big Test Light",
            Capabilities =
            [
                new DimmerCapability("Dimmer", offset: 0),
                new ColorCapability("Color", redOffset: 1, greenOffset: 2, blueOffset: 3),
                new ColorCapability("Color", redOffset: 4, greenOffset: 5, blueOffset: 6),
                new ColorCapability("Color", redOffset: 7, greenOffset: 8, blueOffset: 9),
                new ColorCapability("Color", redOffset: 10, greenOffset: 11, blueOffset: 12),
                new ColorCapability("Color", redOffset: 13, greenOffset: 14, blueOffset: 15),
            ]
        };

        var movingHeadDef = new FixtureDefinition
        {
            Name = "Moving Head",
            Capabilities =
            [
                new PanTiltCapability("Pan/Tilt", panOffset: 0, tiltOffset: 1),
                new DimmerCapability("Dimmer", offset: 2),
                new ColorCapability("Color", redOffset: 3, greenOffset: 4, blueOffset: 5),
            ]
        };

        var encoreDef = new FixtureDefinition
        {
            Name = "Encore Strobe",
            Capabilities =
            [
                new DimmerCapability("Strobe", offset: 0),
                new DimmerFineCapability("Dimmer", offset: 1, fineOffset: 2),
                new ColorCapability("Color", redOffset: 4, greenOffset: 5, blueOffset: 3),
                new DimmerCapability("CTO", offset: 6),

                new PanTiltFineCapability("Pan/Tilt",
                    panOffset: 28, panFineOffset: 29, tiltOffset: 30, tiltFineOffset: 31,
                    defaultPan: 32768, defaultTilt: 32768),

                new DimmerCapability("Effect", offset: 33),
            ]
        };

        return new ShowService
        {
            Universes = [universe],
            Fixtures =
            [
                // new Fixture("Fixture 1", channel: 0,  dimmerDef)       { UniverseNumber = 1 },
                // new Fixture("Fixture 2", channel: 1,  rgbDef)          { UniverseNumber = 1 },
                // new Fixture("Fixture 3", channel: 10, bigTestLightDef) { UniverseNumber = 1 },
                // new Fixture("Moving Head", channel: 26, movingHeadDef) { UniverseNumber = 1 },
                new Fixture("Encore Strobe", channel: 0, encoreDef) { UniverseNumber = 1 },
            ]
        };
    }
}
