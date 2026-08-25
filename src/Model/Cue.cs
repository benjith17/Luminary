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

    // What this cue holds. Snapshot cues use Fixtures; chase cues use Chase.Steps.
    public CueType Type { get; set; } = CueType.Snapshot;

    // Snapshot content: the single look this cue fades to. Unused by chase cues.
    public List<CueFixtureSnapshot> Fixtures { get; set; } = [];

    // Chase content. Always present so the editor can bind to it; only meaningful when
    // Type is Chase.
    public Chase Chase { get; set; } = new();

    // Keyframe content, likewise always present and only meaningful when Type is Keys.
    public KeyframeSequence Keys { get; set; } = new();

    public string DisplayNumber => CueMinor == 0 ? $"{CueMajor}" : $"{CueMajor}.{CueMinor}";
}
