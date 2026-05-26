namespace DMXUniverse;

public abstract class FixtureCapability(string name, int offset, byte defaultValue = 0)
{
    public string Name { get; set; } = name;
    public int Offset { get; set; } = offset;
    public byte Default { get; set; } = defaultValue;
}