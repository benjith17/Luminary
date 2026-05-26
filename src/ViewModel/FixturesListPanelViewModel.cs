using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Model;

namespace ViewModel;

public partial class FixturesListPanelViewModel(ShowService showService) : ViewModelBase
{
    [ObservableProperty]
    public partial FixtureListItemViewModel? SelectedFixture { get; set; }

    public ObservableCollection<FixtureListItemViewModel> Fixtures { get; } = new(
        showService.Fixtures.Select(f => new FixtureListItemViewModel(f, showService))
    );
}
