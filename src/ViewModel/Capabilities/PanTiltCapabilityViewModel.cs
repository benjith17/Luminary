using System.Collections.Generic;
using Model;

namespace ViewModel;

public class PanTiltCapabilityViewModel : CapabilityViewModelBase
{
    public CapabilityParameter Pan { get; }
    public CapabilityParameter Tilt { get; }

    public PanTiltCapabilityViewModel(PanTiltCapability capability, Fixture fixture, ShowService showService)
        : base(capability.Name)
    {
        Pan = new CapabilityParameter(255, 1, MergeMode.Ltp, FadeBehavior.Snap,
            v => showService.GetUniverse(fixture.UniverseNumber)
                ?.Set(fixture.Channel + capability.Offset, (byte)v))
        {
            Manual = capability.Default
        };

        Tilt = new CapabilityParameter(255, 1, MergeMode.Ltp, FadeBehavior.Snap,
            v => showService.GetUniverse(fixture.UniverseNumber)
                ?.Set(fixture.Channel + capability.TiltOffset, (byte)v))
        {
            Manual = capability.DefaultTilt
        };

        Parameters = [Pan, Tilt];
    }

    protected override IReadOnlyList<CapabilityParameter> Parameters { get; }
}
