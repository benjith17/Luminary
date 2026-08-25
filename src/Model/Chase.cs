namespace Model;

// What kind of content a cue holds. Snapshot is the classic single-look cue; Chase loops a list
// of looks. Absent from older show files, which load as Snapshot.
public enum CueType
{
    Snapshot,
    Chase,
    Keys
}

// Chase content of a cue: an endless loop over its steps, started by GO and stopped by the next
// GO (which leaves the values where they were, so the incoming cue fades out of them).
public class Chase
{
    // Crossfade into each step. The cue's own FadeIn covers the entry into the first step, so a
    // chase can ease in slowly and still snap between steps. Clamped to the step's duration.
    public TimeSpan StepFade { get; set; } = TimeSpan.Zero;

    public List<ChaseStep> Steps { get; set; } = [];
}

// One step of a chase: a full stage snapshot plus how long the chase rests on it before moving on.
// Duration covers the whole step including its fade in, so a 1s step with a 1s fade fades the
// entire time.
public class ChaseStep
{
    public TimeSpan Duration { get; set; } = TimeSpan.FromSeconds(1);
    public List<CueFixtureSnapshot> Fixtures { get; set; } = [];
}
