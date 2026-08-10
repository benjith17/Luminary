namespace Model;

public class Fixture(string name, int channel, FixtureDefinition fixtureType)
{
    // Stable identity, independent of name/address, so cues keep matching after edits.
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = name;
    public byte UniverseNumber { get; set; }
    public int Channel { get; set; } = channel;

    public FixtureDefinition FixtureType { get; init; } = fixtureType;

    public void Update(byte[] universeChannels) {}
}