using CommunityToolkit.Mvvm.ComponentModel;
using Model;

namespace ViewModel;

public partial class CueViewModel(Cue cue) : ViewModelBase
{
    public Cue Model { get; } = cue;

    public string DisplayNumber => cue.DisplayNumber;
    public string Label => cue.Label;

    [ObservableProperty]
    public partial bool IsActive { get; set; }
}
