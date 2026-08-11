namespace Model;

public class DimmerCapability(string name, int offset, byte defaultValue = 0)
    : FixtureCapability(name, offset, defaultValue)
{
    public override string MacroName => "Dimmer";
    public override IReadOnlyList<string> MacroParameters { get; } = ["Level"];
}
