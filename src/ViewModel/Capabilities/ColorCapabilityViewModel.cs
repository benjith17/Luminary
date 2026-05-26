using CommunityToolkit.Mvvm.ComponentModel;
using Model;

namespace ViewModel;

public partial class ColorCapabilityViewModel : CapabilityViewModelBase
{
    private readonly ColorCapability _capability;
    private readonly Fixture _fixture;
    private readonly ShowService _showService;

    public ColorCapabilityViewModel(ColorCapability capability, Fixture fixture, ShowService showService)
        : base(capability.Name)
    {
        _capability = capability;
        _fixture = fixture;
        _showService = showService;
    }

    [ObservableProperty]
    public partial byte Red { get; set; }

    [ObservableProperty]
    public partial byte Green { get; set; }

    [ObservableProperty]
    public partial byte Blue { get; set; }

    partial void OnRedChanged(byte value) =>
        _showService.GetUniverse(_fixture.UniverseNumber)?.Set(_fixture.Channel + _capability.Offset, value);

    partial void OnGreenChanged(byte value) =>
        _showService.GetUniverse(_fixture.UniverseNumber)?.Set(_fixture.Channel + _capability.GreenOffset, value);

    partial void OnBlueChanged(byte value) =>
        _showService.GetUniverse(_fixture.UniverseNumber)?.Set(_fixture.Channel + _capability.BlueOffset, value);
}
