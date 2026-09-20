namespace Model;

// A single 8-bit channel that is not an intensity: zoom, shutter, CTO, a colour wheel, a control
// channel. Previously these were all DimmerCapability, which made every one of them answer to the
// macro name "Dimmer" — so `set dimmer` and `@ <one value>` hit whichever came first in the
// personality rather than the actual dimmer.
//
// A level is addressable by its own name instead, so `set zoom 50` works and `set dimmer` can only
// mean the dimmer. Names containing spaces are not addressable, since macro identifiers are
// letters and digits only.
public class LevelCapability(string name, int offset, byte defaultValue = 0)
    : FixtureCapability(name, offset, defaultValue)
{
    // A level is whatever the personality says it is, so the family it was declared in decides:
    // one grouped under <intensity> is a second dimmer and goes dark, while a shutter, a control
    // channel or a colour wheel holds. A file overrides either way with blackout="zero"/"hold".
    public override BlackoutBehavior DefaultBlackout =>
        Family == CapabilityFamily.Intensity ? BlackoutBehavior.Zero : BlackoutBehavior.Hold;

    public override string MacroName => Name;
    public override IReadOnlyList<string> MacroParameters { get; } = ["Level"];
}

// The 16-bit form: a coarse channel paired with a fine channel.
public class LevelFineCapability(string name, int offset, int fineOffset, ushort defaultValue = 0)
    : FixtureCapability(name, offset, (byte)(defaultValue >> 8))
{
    public int FineOffset { get; } = fineOffset;
    public ushort DefaultFine { get; } = defaultValue;

    public override IEnumerable<int> Channels => [Offset, FineOffset];

    public override BlackoutBehavior DefaultBlackout =>
        Family == CapabilityFamily.Intensity ? BlackoutBehavior.Zero : BlackoutBehavior.Hold;

    public override string MacroName => Name;
    public override IReadOnlyList<string> MacroParameters { get; } = ["Level"];
}
