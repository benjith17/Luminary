using CommunityToolkit.Mvvm.ComponentModel;

namespace ViewModel;

public partial class FixtureEditorViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial FixtureListItemViewModel? SelectedFixture { get; set; }
}