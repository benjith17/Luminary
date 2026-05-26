using CommunityToolkit.Mvvm.ComponentModel;

namespace ViewModel;

public partial class FixtureListItemViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial string Name { get; set; }

    [ObservableProperty]
    public partial bool IsEnabled { get; set; }

    public FixtureListItemViewModel(string name, bool isEnabled = false)
    {
        Name = name;
        IsEnabled = isEnabled;
    }
}
