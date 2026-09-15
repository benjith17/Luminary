using System.Collections.Generic;
using Model;

namespace ViewModel;

// A generic single channel: zoom, shutter, CTO, a colour wheel, a control channel.
//
// Merged LTP rather than HTP: a level is not an intensity, so "highest wins" is meaningless for it.
// A cue setting zoom to 0 must be able to override a manual zoom of 255, which HTP would prevent.
public class LevelCapabilityViewModel : CapabilityViewModelBase
{
    public CapabilityParameter Level { get; }

    public LevelCapabilityViewModel(LevelCapability capability, Fixture fixture, ShowService showService)
        : base(capability)
    {
        Level = new CapabilityParameter(
            max: 255, width: 1, MergeMode.Ltp, FadeBehavior.Fade,
            v => showService.GetUniverse(fixture.UniverseNumber)
                ?.Set(fixture.Channel + capability.Offset, (byte)v))
        {
            Manual = capability.Default
        };

        Parameters = [Level];
    }

    protected override IReadOnlyList<CapabilityParameter> Parameters { get; }
}

// The 16-bit form: a coarse channel paired with a fine channel.
public class LevelFineCapabilityViewModel : CapabilityViewModelBase
{
    public CapabilityParameter Level { get; }

    public LevelFineCapabilityViewModel(LevelFineCapability capability, Fixture fixture, ShowService showService)
        : base(capability)
    {
        Level = new CapabilityParameter(
            max: 65535, width: 2, MergeMode.Ltp, FadeBehavior.Fade,
            v =>
            {
                var universe = showService.GetUniverse(fixture.UniverseNumber);
                universe?.Set(fixture.Channel + capability.Offset,     (byte)(v >> 8));
                universe?.Set(fixture.Channel + capability.FineOffset, (byte)(v & 0xFF));
            })
        {
            Manual = capability.DefaultFine
        };

        Parameters = [Level];
    }

    protected override IReadOnlyList<CapabilityParameter> Parameters { get; }
}
