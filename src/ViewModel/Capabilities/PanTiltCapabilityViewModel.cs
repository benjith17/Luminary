using CommunityToolkit.Mvvm.ComponentModel;
using Model;

namespace ViewModel;

public partial class PanTiltCapabilityViewModel : CapabilityViewModelBase
{
    private readonly PanTiltCapability _capability;
    private readonly Fixture _fixture;
    private readonly ShowService _showService;

    public PanTiltCapabilityViewModel(PanTiltCapability capability, Fixture fixture, ShowService showService)
        : base(capability.Name)
    {
        _capability = capability;
        _fixture = fixture;
        _showService = showService;
        Pan  = capability.Default;
        Tilt = capability.DefaultTilt;
    }

    [ObservableProperty]
    public partial byte Pan { get; set; }

    [ObservableProperty]
    public partial byte Tilt { get; set; }

    partial void OnPanChanged(byte value) =>
        _showService.GetUniverse(_fixture.UniverseNumber)?.Set(_fixture.Channel + _capability.Offset, value);

    partial void OnTiltChanged(byte value) =>
        _showService.GetUniverse(_fixture.UniverseNumber)?.Set(_fixture.Channel + _capability.TiltOffset, value);
}
