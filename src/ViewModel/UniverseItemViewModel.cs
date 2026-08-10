using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Model;

namespace ViewModel;

public enum UniverseOutputType
{
    ArtNet,
    Disabled
}

public partial class UniverseItemViewModel : ViewModelBase
{
    private readonly Universe _universe;

    public UniverseItemViewModel(Universe universe)
    {
        _universe = universe;
        OutputType = universe.Output is ArtNetOutput ? UniverseOutputType.ArtNet : UniverseOutputType.Disabled;
    }

    public Universe Universe => _universe;
    public byte Number => _universe.Number;

    public UniverseOutputType[] OutputTypes { get; } = Enum.GetValues<UniverseOutputType>();

    // One-line description for the master list.
    public string Summary => _universe.Output switch
    {
        ArtNetOutput a => $"Art-Net · {a.Ip}",
        _ => "Disabled"
    };

    [ObservableProperty]
    public partial UniverseOutputType OutputType { get; set; }

    public bool ShowArtNetSettings => OutputType == UniverseOutputType.ArtNet;

    public string Ip
    {
        get => (_universe.Output as ArtNetOutput)?.Ip ?? string.Empty;
        set
        {
            if (_universe.Output is not ArtNetOutput a) return;
            if (SetProperty(a.Ip, value, a, (o, v) => o.Ip = v))
                OnPropertyChanged(nameof(Summary));
        }
    }

    public int Port
    {
        get => (_universe.Output as ArtNetOutput)?.Port ?? 6454;
        set
        {
            if (_universe.Output is ArtNetOutput a)
                SetProperty(a.Port, value, a, (o, v) => o.Port = v);
        }
    }

    public int ArtNetUniverse
    {
        get => (_universe.Output as ArtNetOutput)?.ArtNetUniverse ?? 0;
        set
        {
            if (_universe.Output is ArtNetOutput a)
                SetProperty(a.ArtNetUniverse, value, a, (o, v) => o.ArtNetUniverse = v);
        }
    }

    partial void OnOutputTypeChanged(UniverseOutputType value)
    {
        _universe.Output = value == UniverseOutputType.ArtNet
            ? _universe.Output as ArtNetOutput ?? new ArtNetOutput()
            : null;

        OnPropertyChanged(nameof(ShowArtNetSettings));
        OnPropertyChanged(nameof(Ip));
        OnPropertyChanged(nameof(Port));
        OnPropertyChanged(nameof(ArtNetUniverse));
        OnPropertyChanged(nameof(Summary));
    }
}
