using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Model;

namespace ViewModel;

// One key on one track, as shown in the timeline.
public partial class KeyframeViewModel : ViewModelBase
{
    private readonly CapabilityViewModelBase _capability;

    public KeyframeViewModel(Keyframe model, CapabilityViewModelBase capability)
    {
        Model = model;
        _capability = capability;
    }

    public Keyframe Model { get; }

    public double TimeSeconds => Model.Time.TotalSeconds;

    public Interpolation Interpolation
    {
        get => Model.Interpolation;
        set
        {
            if (Model.Interpolation == value) return;
            Model.Interpolation = value;
            OnPropertyChanged();
        }
    }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    // Colour of the key's marker, so a track reads at a glance. A three-byte capability is taken
    // as RGB and shown as itself; anything else is shown as a grey ramp of its first parameter,
    // which is the closest thing to "how much" a generic capability has.
    public Color Swatch
    {
        get
        {
            var values = Model.Values;

            if (values.Length == 3 && _capability is ColorCapabilityViewModel)
                return Color.FromRgb(values[0], values[1], values[2]);

            var level = values.Length == 0 ? (byte)0 : values[0];
            return Color.FromRgb(level, level, level);
        }
    }

    // Re-recording a key changes its value and therefore its marker.
    public void NotifyValueChanged() => OnPropertyChanged(nameof(Swatch));

    public void NotifyTimeChanged() => OnPropertyChanged(nameof(TimeSeconds));
}
