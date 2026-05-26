namespace Model;

public class DimmerCapability(string name, int offset, byte defaultValue = 0)
    : FixtureCapability(name, offset, defaultValue);
