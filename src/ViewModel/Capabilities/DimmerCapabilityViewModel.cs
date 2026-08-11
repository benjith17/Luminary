using System.Collections.Generic;
using Model;

namespace ViewModel;

public class DimmerCapabilityViewModel : CapabilityViewModelBase
{
    public CapabilityParameter Intensity { get; }

    public DimmerCapabilityViewModel(DimmerCapability capability, Fixture fixture, ShowService showService)
        : base(capability)
    {
        Intensity = new CapabilityParameter(
            max: 255, width: 1, MergeMode.Htp, FadeBehavior.Fade,
            v => showService.GetUniverse(fixture.UniverseNumber)
                ?.Set(fixture.Channel + capability.Offset, (byte)v))
        {
            Manual = capability.Default
        };

        Parameters = [Intensity];
    }

    protected override IReadOnlyList<CapabilityParameter> Parameters { get; }
}
