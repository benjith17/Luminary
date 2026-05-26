using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ViewModel;

public partial class EffectViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial string Name { get; set; }

    [ObservableProperty]
    public partial bool IsEnabled { get; set; }

    public EffectViewModel(string name, bool isEnabled = false)
    {
        Name = name;
        IsEnabled = isEnabled;
    }

    [RelayCommand]
    private void AddEffect()
    {}
}
