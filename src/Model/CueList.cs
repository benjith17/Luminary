namespace Model;

public class CueList
{
    public string Name { get; set; } = string.Empty;
    public List<Cue> Cues { get; set; } = [];
    public int ActiveIndex { get; set; } = -1;
}
