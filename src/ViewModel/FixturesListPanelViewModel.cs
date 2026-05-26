using CommunityToolkit.Mvvm.ComponentModel;

namespace ViewModel;

public partial class FixturesListPanelViewModel : ViewModelBase
{
    [ObservableProperty]
    private List<FixtureListItemViewModel> fixtures = [
        new FixtureListItemViewModel("Fixture 1"),
        new FixtureListItemViewModel("Fixture 2"),
        new FixtureListItemViewModel("Fixture 3"),
    ];
}
