namespace Model;

// How a keyframe reaches the one after it.
public enum Interpolation
{
    Linear,     // constant-rate ramp
    Hold,       // stay put, then jump at the next key (wheels, mode channels, hard hits)
    EaseIn,     // start slow, arrive at full speed
    EaseOut,    // leave at full speed, settle gently
    EaseInOut   // slow at both ends
}

// One authored value on a track, at a point in time. Values is capability-shaped — the same byte
// layout Capture() produces and ApplyValues() consumes, so a key is literally "what this
// capability looked like".
public class Keyframe
{
    public TimeSpan Time { get; set; }
    public byte[] Values { get; set; } = [];

    // Governs the segment that *leaves* this key. The last key's setting is unused.
    public Interpolation Interpolation { get; set; } = Interpolation.Linear;
}

// One capability of one fixture, automated over time. Addressed the same way cue snapshots are —
// by stable fixture id — plus the capability's index within that fixture's personality, which is
// fixed for the life of the fixture because its type cannot change after patching.
public class KeyframeTrack
{
    public Guid FixtureId { get; set; }
    public int CapabilityIndex { get; set; }

    // Kept sorted by Time. The editor re-sorts after any move.
    public List<Keyframe> Keys { get; set; } = [];
}

// Keyframe content of a cue: a fixed-length timeline of independently automated tracks.
public class KeyframeSequence
{
    // Length of the timeline. Doubles as the loop point and the editor's ruler extent; keys are
    // not placed beyond it.
    public TimeSpan Duration { get; set; } = TimeSpan.FromSeconds(10);

    // When false the sequence runs once and holds its final values. When true it wraps at Duration.
    public bool Loop { get; set; }

    public List<KeyframeTrack> Tracks { get; set; } = [];
}
