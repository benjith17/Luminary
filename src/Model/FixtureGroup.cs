namespace Model;

public class FixtureGroup
{
    public string Name { get; set; } = string.Empty;
    public List<FixtureCapability> Capabilities { get; set; } = [];
}