namespace Model;

public class Fixture(string name, int channel, FixtureDefinition fixtureType)
{
    public string Name { get; set; } = name;
    public byte UniverseNumber { get; set; }
    public int Channel { get; set; } = channel;

    public FixtureDefinition FixtureType { get; init; } = fixtureType;

    public void Update(byte[] universeChannels) {}
}