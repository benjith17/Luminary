using System.Collections.Generic;
using Model;

namespace ViewModel;

public class PanTiltFineCapabilityViewModel : CapabilityViewModelBase
{
    public CapabilityParameter Pan { get; }
    public CapabilityParameter Tilt { get; }

    public PanTiltFineCapabilityViewModel(PanTiltFineCapability capability, Fixture fixture, ShowService showService)
        : base(capability.Name)
    {
        Pan = new CapabilityParameter(65535, 2, MergeMode.Ltp, FadeBehavior.Snap,
            v =>
            {
                var universe = showService.GetUniverse(fixture.UniverseNumber);
                universe?.Set(fixture.Channel + capability.Offset,        (byte)(v >> 8));
                universe?.Set(fixture.Channel + capability.PanFineOffset, (byte)(v & 0xFF));
            })
        {
            Manual = capability.DefaultPan
        };

        Tilt = new CapabilityParameter(65535, 2, MergeMode.Ltp, FadeBehavior.Snap,
            v =>
            {
                var universe = showService.GetUniverse(fixture.UniverseNumber);
                universe?.Set(fixture.Channel + capability.TiltOffset,     (byte)(v >> 8));
                universe?.Set(fixture.Channel + capability.TiltFineOffset, (byte)(v & 0xFF));
            })
        {
            Manual = capability.DefaultTilt
        };

        Parameters = [Pan, Tilt];
    }

    protected override IReadOnlyList<CapabilityParameter> Parameters { get; }
}
