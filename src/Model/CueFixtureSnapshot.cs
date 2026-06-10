namespace Model;

public class CueFixtureSnapshot
{
    public string FixtureName { get; set; } = string.Empty;
    public List<byte[]> CapabilityValues { get; set; } = [];
}
