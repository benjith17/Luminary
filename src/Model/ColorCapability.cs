namespace Model;

// Offset (inherited) maps to the red channel.
public class ColorCapability(
    string name, int redOffset, int greenOffset, int blueOffset,
    byte defaultRed = 0, byte defaultGreen = 0, byte defaultBlue = 0)
    : FixtureCapability(name, redOffset, defaultRed)
{
    public int  GreenOffset   { get; set; } = greenOffset;
    public int  BlueOffset    { get; set; } = blueOffset;
    public byte DefaultGreen  { get; set; } = defaultGreen;
    public byte DefaultBlue   { get; set; } = defaultBlue;

    public override IEnumerable<int> Channels => [Offset, GreenOffset, BlueOffset];

    // Killed alongside the dimmer rather than instead of it: a fixture may have no dimmer at all,
    // or a virtual one that never quite reaches zero, and since blackout only masks the transmitted
    // frame the colour comes straight back on release.
    public override BlackoutBehavior DefaultBlackout => BlackoutBehavior.Zero;

    public override string MacroName => "Color";
    public override IReadOnlyList<string> MacroParameters { get; } = ["R", "G", "B"];
}
