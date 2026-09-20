namespace Model;

public class DimmerCapability(string name, int offset, byte defaultValue = 0)
    : FixtureCapability(name, offset, defaultValue)
{
    // The fixture's intensity: exactly what blackout exists to kill.
    public override BlackoutBehavior DefaultBlackout => BlackoutBehavior.Zero;

    public override string MacroName => "Dimmer";
    public override IReadOnlyList<string> MacroParameters { get; } = ["Level"];
}
