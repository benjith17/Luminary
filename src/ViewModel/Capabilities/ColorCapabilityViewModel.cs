using System.Collections.Generic;
using Model;

namespace ViewModel;

public class ColorCapabilityViewModel : CapabilityViewModelBase
{
    public CapabilityParameter Red { get; }
    public CapabilityParameter Green { get; }
    public CapabilityParameter Blue { get; }

    public ColorCapabilityViewModel(ColorCapability capability, Fixture fixture, ShowService showService)
        : base(capability.Name)
    {
        Red = new CapabilityParameter(255, 1, MergeMode.Htp, FadeBehavior.Fade,
            v => showService.GetUniverse(fixture.UniverseNumber)
                ?.Set(fixture.Channel + capability.Offset, (byte)v))
        {
            Manual = capability.Default
        };

        Green = new CapabilityParameter(255, 1, MergeMode.Htp, FadeBehavior.Fade,
            v => showService.GetUniverse(fixture.UniverseNumber)
                ?.Set(fixture.Channel + capability.GreenOffset, (byte)v))
        {
            Manual = capability.DefaultGreen
        };

        Blue = new CapabilityParameter(255, 1, MergeMode.Htp, FadeBehavior.Fade,
            v => showService.GetUniverse(fixture.UniverseNumber)
                ?.Set(fixture.Channel + capability.BlueOffset, (byte)v))
        {
            Manual = capability.DefaultBlue
        };

        Parameters = [Red, Green, Blue];
    }

    protected override IReadOnlyList<CapabilityParameter> Parameters { get; }
}
