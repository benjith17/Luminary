namespace Model;

public class Cue
{
    public int CueMajor { get; set; }
    public int CueMinor { get; set; }
    public string Label { get; set; } = string.Empty;
    public TimeSpan FadeIn { get; set; } = TimeSpan.Zero;
    public List<CueFixtureSnapshot> Fixtures { get; set; } = [];

    public string DisplayNumber => CueMinor == 0 ? $"{CueMajor}" : $"{CueMajor}.{CueMinor}";
}
