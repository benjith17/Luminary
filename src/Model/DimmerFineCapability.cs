namespace Model;

public class DimmerFineCapability(string name, int offset, int fineOffset, ushort defaultValue = 0)
    : FixtureCapability(name, offset, (byte)(defaultValue >> 8))
{
    public int FineOffset { get; } = fineOffset;
    public ushort DefaultFine { get; } = defaultValue;

    public override IEnumerable<int> Channels => [Offset, FineOffset];

    public override string MacroName => "Dimmer";
    public override IReadOnlyList<string> MacroParameters { get; } = ["Level"];
}
