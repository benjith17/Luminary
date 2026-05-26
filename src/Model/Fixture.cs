namespace Model;

public class Fixture(string name, ushort channel, FixtureDefinition fixtureType)
{
    public string Name { get; set; } = name;
    public ushort Channel { get; set; } = channel;

    public FixtureDefinition FixtureType { get; init; } = fixtureType;

    public void Update(byte[] universeChannels) {}
}