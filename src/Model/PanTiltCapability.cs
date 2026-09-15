namespace Model;

// Offset (inherited) maps to the pan channel.
public class PanTiltCapability(string name, int panOffset, int tiltOffset, byte defaultPan = 0, byte defaultTilt = 0)
    : FixtureCapability(name, panOffset, defaultPan)
{
    public int  TiltOffset   { get; set; } = tiltOffset;
    public byte DefaultTilt  { get; set; } = defaultTilt;

    public override IEnumerable<int> Channels => [Offset, TiltOffset];

    public override string MacroName => "Position";
    public override IReadOnlyList<string> MacroParameters { get; } = ["Pan", "Tilt"];
}
