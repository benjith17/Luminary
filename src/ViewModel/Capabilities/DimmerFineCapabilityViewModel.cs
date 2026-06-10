using CommunityToolkit.Mvvm.ComponentModel;
using Model;

namespace ViewModel;

public partial class DimmerFineCapabilityViewModel : CapabilityViewModelBase
{
    private readonly DimmerFineCapability _capability;
    private readonly Fixture _fixture;
    private readonly ShowService _showService;

    public DimmerFineCapabilityViewModel(DimmerFineCapability capability, Fixture fixture, ShowService showService)
        : base(capability.Name)
    {
        _capability = capability;
        _fixture = fixture;
        _showService = showService;
        Value = capability.DefaultFine;
    }

    [ObservableProperty]
    public partial ushort Value { get; set; }

    partial void OnValueChanged(ushort value)
    {
        var universe = _showService.GetUniverse(_fixture.UniverseNumber);
        universe?.Set(_fixture.Channel + _capability.Offset,     (byte)(value >> 8));
        universe?.Set(_fixture.Channel + _capability.FineOffset, (byte)(value & 0xFF));
    }

    public override byte[] Capture() => [(byte)(Value >> 8), (byte)(Value & 0xFF)];
    public override void Restore(byte[] values) => Value = (ushort)((values[0] << 8) | values[1]);
}
