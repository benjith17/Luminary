namespace Model;

public class FixtureDefinition
{
    // Unique, stable identifier used to reference this personality from a saved show.
    public string Name { get; set; } = string.Empty;

    // Hierarchy for the fixture picker: Manufacturer → Model → Mode (personality).
    public string Manufacturer { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Mode { get; set; } = string.Empty;

    // Number of DMX channels this personality occupies, used for patching / addressing.
    public int ChannelCount { get; set; }

    public List<FixtureCapability> Capabilities { get; set; } = [];
}