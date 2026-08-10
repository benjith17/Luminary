using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Model;

namespace ViewModel;

public enum UniverseOutputType
{
    ArtNet,   // Art-Net 3 — broadcast
    ArtNet4,  // Art-Net 4 — discovery + unicast
    Disabled
}

public partial class UniverseItemViewModel : ViewModelBase
{
    private readonly Universe _universe;

    public UniverseItemViewModel(Universe universe)
    {
        _universe = universe;
        OutputType = universe.Output switch
        {
            ArtNet4Output => UniverseOutputType.ArtNet4,
            ArtNetOutput => UniverseOutputType.ArtNet,
            _ => UniverseOutputType.Disabled
        };
    }

    public Universe Universe => _universe;
    public byte Number => _universe.Number;

    public UniverseOutputType[] OutputTypes { get; } = Enum.GetValues<UniverseOutputType>();

    // One-line description for the master list.
    public string Summary => _universe.Output switch
    {
        ArtNet4Output a => $"Art-Net 4 · universe {a.ArtNetUniverse}",
        ArtNetOutput a => $"Art-Net · {a.Ip}",
        _ => "Disabled"
    };

    [ObservableProperty]
    public partial UniverseOutputType OutputType { get; set; }

    public bool ShowUniverseSettings => _universe.Output is ArtNetOutput or ArtNet4Output;
    public bool ShowArtNetSettings => _universe.Output is ArtNetOutput;   // v3: destination IP
    public bool ShowArtNet4Settings => _universe.Output is ArtNet4Output; // v4: manual targets

    // Destination IP — Art-Net 3 only.
    public string Ip
    {
        get => (_universe.Output as ArtNetOutput)?.Ip ?? string.Empty;
        set
        {
            if (_universe.Output is not ArtNetOutput a || a.Ip == value) return;
            a.Ip = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Summary));
        }
    }

    // Shared by both Art-Net modes.
    public int Port
    {
        get => PortOf(_universe.Output);
        set
        {
            switch (_universe.Output)
            {
                case ArtNetOutput a: a.Port = value; break;
                case ArtNet4Output a: a.Port = value; break;
                default: return;
            }
            OnPropertyChanged();
        }
    }

    // 15-bit Port-Address, shared by both Art-Net modes. Clamped so it can't overflow the Net byte.
    public int ArtNetUniverse
    {
        get => UniverseOf(_universe.Output);
        set
        {
            var clamped = Math.Clamp(value, 0, 32767);
            if (clamped == UniverseOf(_universe.Output)) return;
            switch (_universe.Output)
            {
                case ArtNetOutput a: a.ArtNetUniverse = clamped; break;
                case ArtNet4Output a: a.ArtNetUniverse = clamped; break;
                default: return;
            }
            OnPropertyChanged();
            OnPropertyChanged(nameof(Summary));
        }
    }

    // Manual unicast targets — Art-Net 4 only. Edited as one IP per line.
    public string ManualTargets
    {
        get => _universe.Output is ArtNet4Output a ? string.Join(Environment.NewLine, a.ManualTargets) : string.Empty;
        set
        {
            if (_universe.Output is not ArtNet4Output a) return;
            a.ManualTargets = [.. value.Split(['\n', '\r', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
            OnPropertyChanged();
        }
    }

    partial void OnOutputTypeChanged(UniverseOutputType value)
    {
        // Preserve the universe number when toggling between the two Art-Net modes.
        _universe.Output = value switch
        {
            UniverseOutputType.ArtNet => _universe.Output as ArtNetOutput ?? new ArtNetOutput { ArtNetUniverse = UniverseOf(_universe.Output) },
            UniverseOutputType.ArtNet4 => _universe.Output as ArtNet4Output ?? new ArtNet4Output { ArtNetUniverse = UniverseOf(_universe.Output) },
            _ => null
        };

        OnPropertyChanged(nameof(ShowUniverseSettings));
        OnPropertyChanged(nameof(ShowArtNetSettings));
        OnPropertyChanged(nameof(ShowArtNet4Settings));
        OnPropertyChanged(nameof(Ip));
        OnPropertyChanged(nameof(Port));
        OnPropertyChanged(nameof(ArtNetUniverse));
        OnPropertyChanged(nameof(ManualTargets));
        OnPropertyChanged(nameof(Summary));
    }

    private static int UniverseOf(UniverseOutput? output) => output switch
    {
        ArtNetOutput a => a.ArtNetUniverse,
        ArtNet4Output a => a.ArtNetUniverse,
        _ => 0
    };

    private static int PortOf(UniverseOutput? output) => output switch
    {
        ArtNetOutput a => a.Port,
        ArtNet4Output a => a.Port,
        _ => 6454
    };
}
