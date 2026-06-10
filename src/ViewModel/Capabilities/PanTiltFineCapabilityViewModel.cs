using CommunityToolkit.Mvvm.ComponentModel;
using Model;

namespace ViewModel;

public partial class PanTiltFineCapabilityViewModel : CapabilityViewModelBase
{
    private readonly PanTiltFineCapability _capability;
    private readonly Fixture _fixture;
    private readonly ShowService _showService;

    public PanTiltFineCapabilityViewModel(PanTiltFineCapability capability, Fixture fixture, ShowService showService)
        : base(capability.Name)
    {
        _capability = capability;
        _fixture = fixture;
        _showService = showService;
        Pan  = capability.DefaultPan;
        Tilt = capability.DefaultTilt;
    }

    [ObservableProperty]
    public partial ushort Pan { get; set; }

    [ObservableProperty]
    public partial ushort Tilt { get; set; }

    partial void OnPanChanged(ushort value)
    {
        var universe = _showService.GetUniverse(_fixture.UniverseNumber);
        universe?.Set(_fixture.Channel + _capability.Offset,      (byte)(value >> 8));
        universe?.Set(_fixture.Channel + _capability.PanFineOffset, (byte)(value & 0xFF));
    }

    partial void OnTiltChanged(ushort value)
    {
        var universe = _showService.GetUniverse(_fixture.UniverseNumber);
        universe?.Set(_fixture.Channel + _capability.TiltOffset,     (byte)(value >> 8));
        universe?.Set(_fixture.Channel + _capability.TiltFineOffset, (byte)(value & 0xFF));
    }

    public override byte[] Capture() =>
        [(byte)(Pan >> 8), (byte)(Pan & 0xFF), (byte)(Tilt >> 8), (byte)(Tilt & 0xFF)];

    public override void Restore(byte[] values)
    {
        Pan  = (ushort)((values[0] << 8) | values[1]);
        Tilt = (ushort)((values[2] << 8) | values[3]);
    }
}
