using System.Collections.Generic;
using Model;

namespace ViewModel;

public class DimmerFineCapabilityViewModel : CapabilityViewModelBase
{
    public CapabilityParameter Intensity { get; }

    public DimmerFineCapabilityViewModel(DimmerFineCapability capability, Fixture fixture, ShowService showService)
        : base(capability)
    {
        Intensity = new CapabilityParameter(
            max: 65535, width: 2, MergeMode.Htp, FadeBehavior.Fade,
            v =>
            {
                var universe = showService.GetUniverse(fixture.UniverseNumber);
                universe?.Set(fixture.Channel + capability.Offset,     (byte)(v >> 8));
                universe?.Set(fixture.Channel + capability.FineOffset, (byte)(v & 0xFF));
            })
        {
            Manual = capability.DefaultFine
        };

        Parameters = [Intensity];
    }

    protected override IReadOnlyList<CapabilityParameter> Parameters { get; }
}
