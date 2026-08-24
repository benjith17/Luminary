namespace Model;

public class Cue
{
    public int CueMajor { get; set; }
    public int CueMinor { get; set; }
    public string Label { get; set; } = string.Empty;
    public TimeSpan FadeIn { get; set; } = TimeSpan.Zero;

    // Free-text operator note (cue calling, reminders). Empty when unset.
    public string Notes { get; set; } = string.Empty;

    // Auto-follow: when set, the next cue fires automatically this long after this cue fires.
    // Null means the cue holds until the next manual GO.
    public TimeSpan? Follow { get; set; }

    public List<CueFixtureSnapshot> Fixtures { get; set; } = [];

    public string DisplayNumber => CueMinor == 0 ? $"{CueMajor}" : $"{CueMajor}.{CueMinor}";
}
