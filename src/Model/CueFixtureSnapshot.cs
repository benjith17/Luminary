namespace Model;

public class CueFixtureSnapshot
{
    public Guid FixtureId { get; set; }
    public List<byte[]> CapabilityValues { get; set; } = [];
}
