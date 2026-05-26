using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Model;

namespace ViewModel;

public partial class FixtureListItemViewModel : ViewModelBase
{
    private readonly Fixture _fixture = null!;

    public string Name => _fixture.Name;

    [ObservableProperty]
    public partial bool IsEnabled { get; set; }

    public ObservableCollection<CapabilityViewModelBase> Capabilities { get; }

    public FixtureListItemViewModel(Fixture fixture, ShowService showService)
    {
        _fixture = fixture;
        Capabilities = new ObservableCollection<CapabilityViewModelBase>(
            fixture.FixtureType.Capabilities.Select(c => CreateCapabilityViewModel(c, fixture, showService))
        );
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
