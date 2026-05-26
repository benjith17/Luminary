namespace Model;

public class FixtureDefinition
{
    public string Name { get; set; } = string.Empty;
    public List<FixtureCapability> Capabilities { get; set; } = [];
}