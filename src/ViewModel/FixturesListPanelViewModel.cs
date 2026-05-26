using CommunityToolkit.Mvvm.ComponentModel;

namespace ViewModel;

public partial class FixturesListPanelViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial FixtureListItemViewModel? SelectedFixture { get; set; }

    [ObservableProperty]
    private List<FixtureListItemViewModel> _fixtures = [
        new FixtureListItemViewModel("Fixture 1"),
        new FixtureListItemViewModel("Fixture 2"),
        new FixtureListItemViewModel("Fixture 3"),
    ];

    public FixturesListPanelViewModel()
    {
        SelectedFixture = Fixtures.FirstOrDefault();
    }
}
