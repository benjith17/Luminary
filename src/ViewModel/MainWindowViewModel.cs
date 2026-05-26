using CommunityToolkit.Mvvm.ComponentModel;

namespace ViewModel;

public partial class MainWindowViewModel : ViewModelBase
{
    public EffectsPanelViewModel EffectsPanel { get; } = new();
    public FixturesListPanelViewModel FixturesListPanel { get; }
    public FixtureEditorViewModel FixtureEditor { get; } = new();

    public MainWindowViewModel()
    {
        FixturesListPanel = new();
        FixturesListPanel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(FixturesListPanelViewModel.SelectedFixture))
                FixtureEditor.SelectedFixture = FixturesListPanel.SelectedFixture;
        };
        FixtureEditor.SelectedFixture = FixturesListPanel.SelectedFixture;
    }
}
