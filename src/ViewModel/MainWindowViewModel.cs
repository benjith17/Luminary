using CommunityToolkit.Mvvm.ComponentModel;
using Model;

namespace ViewModel;

public partial class MainWindowViewModel : ViewModelBase
{
    public EffectsPanelViewModel EffectsPanel { get; } = new();
    public FixturesListPanelViewModel FixturesListPanel { get; }
    public FixtureEditorViewModel FixtureEditor { get; } = new();

    public MainWindowViewModel()
    {
        FixturesListPanel = new(CreateTestShow());

        FixturesListPanel.SelectedFixture = FixturesListPanel.Fixtures.FirstOrDefault();

        FixturesListPanel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(FixturesListPanelViewModel.SelectedFixture))
                FixtureEditor.SelectedFixture = FixturesListPanel.SelectedFixture;
        };

        FixtureEditor.SelectedFixture = FixturesListPanel.SelectedFixture;
    }

    private static ShowService CreateTestShow()
    {
        var universe = new Universe(1);

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

        return new ShowService
        {
            Universes = [universe],
            Fixtures =
            [
                new Fixture("Fixture 1", channel: 0,  dimmerDef) { UniverseNumber = 1 },
                new Fixture("Fixture 2", channel: 1,  rgbDef)    { UniverseNumber = 1 },
                new Fixture("Fixture 3", channel: 10, dimmerDef) { UniverseNumber = 1 },
            ]
        };
    }
}
