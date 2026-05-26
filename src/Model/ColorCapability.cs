namespace Model;

// Offset (inherited) maps to the red channel.
public class ColorCapability(string name, int redOffset, int greenOffset, int blueOffset)
    : FixtureCapability(name, redOffset)
{
    public int GreenOffset { get; set; } = greenOffset;
    public int BlueOffset { get; set; } = blueOffset;
}
