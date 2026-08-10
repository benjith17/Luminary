namespace Model;

public class FixtureDefinition
{
    public string Name { get; set; } = string.Empty;

    // Number of DMX channels this personality occupies, used for patching / addressing.
    public int ChannelCount { get; set; }

    public List<FixtureCapability> Capabilities { get; set; } = [];
}