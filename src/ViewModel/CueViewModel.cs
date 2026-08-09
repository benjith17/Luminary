using CommunityToolkit.Mvvm.ComponentModel;
using Model;

namespace ViewModel;

public partial class CueViewModel(Cue cue) : ViewModelBase
{
    public Cue Model { get; } = cue;

    public string DisplayNumber => Model.DisplayNumber;

    public string Label
    {
        get => Model.Label;
        set => SetProperty(Model.Label, value, Model, (c, v) => c.Label = v);
    }

    // The cue currently live on stage (the last one GO'd). Distinct from list selection.
    [ObservableProperty]
    public partial bool IsActive { get; set; }

    // True while the row's label is being renamed inline.
    [ObservableProperty]
    public partial bool IsEditing { get; set; }

    // Reordering renumbers cues, so the displayed number can change after construction.
    public void NotifyNumberChanged() => OnPropertyChanged(nameof(DisplayNumber));
}
