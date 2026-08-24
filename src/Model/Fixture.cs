namespace Model;

public class Fixture(string name, int channel, FixtureDefinition fixtureType)
{
    // Stable internal identity, independent of everything, so cues keep matching after edits.
    public Guid Id { get; set; } = Guid.NewGuid();

    // User-facing fixture number: short, unique, global, patch-independent. The handle used to
    // reference a fixture (macros, selection). Renumbering is a deliberate act; internal
    // cross-references use Id, not this.
    public int Number { get; set; }

    public string Name { get; set; } = name;
    public byte UniverseNumber { get; set; }
    public int Channel { get; set; } = channel;

    public FixtureDefinition FixtureType { get; init; } = fixtureType;

    public void Update(byte[] universeChannels) {}
}