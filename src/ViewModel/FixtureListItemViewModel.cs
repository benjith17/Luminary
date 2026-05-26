using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ViewModel;

public partial class FixtureListItemViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial string Name { get; set; }

    [ObservableProperty]
    public partial bool IsEnabled { get; set; }

    [RelayCommand]
    public void SelectFixture()
    {
        
    }
    public FixtureListItemViewModel(string name, bool isEnabled = false)
    {
        Name = name;
        IsEnabled = isEnabled;
    }
}
