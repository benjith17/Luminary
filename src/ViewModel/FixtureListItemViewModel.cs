using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Model;

namespace ViewModel;

public partial class FixtureListItemViewModel : ViewModelBase
{
    private readonly Fixture _fixture;
    private readonly ShowService _showService;

    public FixtureListItemViewModel(Fixture fixture, ShowService showService)
    {
        _fixture = fixture;
        _showService = showService;
        Capabilities = new ObservableCollection<CapabilityViewModelBase>(
            fixture.FixtureType.Capabilities.Select(c => CreateCapabilityViewModel(c, fixture, showService))
        );
    }

    public Fixture Fixture => _fixture;

    // User-facing fixture number. Kept unique across the rig: a clashing (or non-positive) value
    // is rejected and the field reverts.
    public int Number
    {
        get => _fixture.Number;
        set
        {
            if (value == _fixture.Number) return;
            if (value < 1 || _showService.Fixtures.Any(f => f != _fixture && f.Number == value))
            {
                OnPropertyChanged();
                return;
            }
            _fixture.Number = value;
            OnPropertyChanged();
        }
    }

    // Raised when a change affects channel assignment (address / universe) so the panel can
    // clear stale channels and re-emit output.
    public event Action? PatchChanged;

    public string Name
    {
        get => _fixture.Name;
        set => SetProperty(_fixture.Name, value, _fixture, (f, v) => f.Name = v);
    }

    public string TypeName => _fixture.FixtureType.Name;

    // Subtitle for the patch list, e.g. "Encore Strobe · U1 · @1".
    public string PatchSummary => $"{TypeName} · U{UniverseNumber} · @{Address}";

    // 1-based DMX address for display; stored 0-based internally.
    public int Address
    {
        get => _fixture.Channel + 1;
        set
        {
            var channel = Math.Clamp(value - 1, 0, 511);
            if (SetProperty(_fixture.Channel, channel, _fixture, (f, v) => f.Channel = v))
            {
                OnPropertyChanged(nameof(PatchSummary));
                PatchChanged?.Invoke();
            }
        }
    }

    public byte UniverseNumber
    {
        get => _fixture.UniverseNumber;
        set
        {
            if (SetProperty(_fixture.UniverseNumber, value, _fixture, (f, v) => f.UniverseNumber = v))
            {
                OnPropertyChanged(nameof(PatchSummary));
                PatchChanged?.Invoke();
            }
        }
    }

    [ObservableProperty]
    public partial bool IsEnabled { get; set; }

    public ObservableCollection<CapabilityViewModelBase> Capabilities { get; }

    public void PushOutput()
    {
        foreach (var capability in Capabilities) capability.PushOutput();
    }

    private static CapabilityViewModelBase CreateCapabilityViewModel(
        FixtureCapability capability, Fixture fixture, ShowService showService) =>
        capability switch
        {
            DimmerFineCapability df     => new DimmerFineCapabilityViewModel(df, fixture, showService),
            DimmerCapability d          => new DimmerCapabilityViewModel(d, fixture, showService),
            ColorCapability c           => new ColorCapabilityViewModel(c, fixture, showService),
            PanTiltFineCapability ptf   => new PanTiltFineCapabilityViewModel(ptf, fixture, showService),
            PanTiltCapability pt        => new PanTiltCapabilityViewModel(pt, fixture, showService),
            _ => throw new NotSupportedException($"No editor for capability type {capability.GetType().Name}")
        };
}
