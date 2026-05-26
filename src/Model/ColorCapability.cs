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
}
