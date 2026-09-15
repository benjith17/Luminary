namespace Model;

// Offset (inherited) = pan coarse. Four channels total: pan coarse, pan fine, tilt coarse, tilt fine.
public class PanTiltFineCapability(
    string name, int panOffset, int panFineOffset, int tiltOffset, int tiltFineOffset,
    ushort defaultPan = 0, ushort defaultTilt = 0)
    : FixtureCapability(name, panOffset, (byte)(defaultPan >> 8))
{
    public int    PanFineOffset  { get; set; } = panFineOffset;
    public int    TiltOffset     { get; set; } = tiltOffset;
    public int    TiltFineOffset { get; set; } = tiltFineOffset;
    public ushort DefaultPan     { get; set; } = defaultPan;
    public ushort DefaultTilt    { get; set; } = defaultTilt;

    public override IEnumerable<int> Channels => [Offset, PanFineOffset, TiltOffset, TiltFineOffset];

    // Positions must not sweep the rig across the stage when a cue is recalled.
    public override FadeBehavior DefaultFade => FadeBehavior.Snap;

    public override string MacroName => "Position";
    public override IReadOnlyList<string> MacroParameters { get; } = ["Pan", "Tilt"];
}
