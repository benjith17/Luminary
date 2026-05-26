using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;

namespace ViewModel;

public partial class EffectsPanelViewModel : ViewModelBase
{
    public ObservableCollection<EffectViewModel> Effects { get; } = [
        new("Fade In"),
        new("Color Cycle", isEnabled: true),
        new("Strobe"),
    ];
}
