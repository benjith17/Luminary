using CommunityToolkit.Mvvm.ComponentModel;
using Model;

namespace ViewModel;

public partial class DimmerCapabilityViewModel : CapabilityViewModelBase
{
    private readonly DimmerCapability _capability;
    private readonly Fixture _fixture;
    private readonly ShowService _showService;

    public DimmerCapabilityViewModel(DimmerCapability capability, Fixture fixture, ShowService showService)
        : base(capability.Name)
    {
        _capability = capability;
        _fixture = fixture;
        _showService = showService;
        Value = capability.Default;
    }

    [ObservableProperty]
    public partial byte Value { get; set; }

    partial void OnValueChanged(byte value) =>
        _showService.GetUniverse(_fixture.UniverseNumber)?.Set(_fixture.Channel + _capability.Offset, value);

    public override byte[] Capture() => [Value];
    public override void Restore(byte[] values) => Value = values[0];
}
