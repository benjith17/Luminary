namespace Persistence;

// The serialized shape of a show file. Kept separate from the runtime model so the file is a
// stable contract: no live channel data, personalities referenced by name, outputs discriminated
// by type, fixtures referenced by stable id.

public sealed class ShowFileDto
{
    // 1: snapshot cues only. 2: adds cue Type / Chase. 3: adds Keys. Older files load unchanged —
    // an absent type reads as a snapshot cue.
    public int Version { get; set; } = 3;
    public List<UniverseDto> Universes { get; set; } = [];
    public List<FixtureDto> Fixtures { get; set; } = [];
    public CueListDto CueList { get; set; } = new();
    public List<MacroDto> Macros { get; set; } = [];
    public List<BindingDto> Bindings { get; set; } = [];
}

public sealed class MacroDto
{
    public string Name { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
}

public sealed class BindingDto
{
    public string Name { get; set; } = string.Empty;
    public BindingTriggerDto Trigger { get; set; } = new();
    public string Action { get; set; } = string.Empty;
}

public sealed class BindingTriggerDto
{
    public string Kind { get; set; } = "key";
    public string? Gesture { get; set; }
    public string? Device { get; set; }
    public int? Channel { get; set; }
    public string? Message { get; set; }
    public int? Number { get; set; }
    public bool IgnoreZero { get; set; }
}

public sealed class UniverseDto
{
    public byte Number { get; set; }
    public OutputDto? Output { get; set; } // null = disabled
}

public sealed class OutputDto
{
    public string Type { get; set; } = "artnet";       // "artnet" (v3 broadcast) | "artnet4" (v4 unicast)
    public string? Ip { get; set; }                     // v3 only
    public int? Port { get; set; }
    public int? ArtNetUniverse { get; set; }
    public List<string>? ManualTargets { get; set; }    // v4 only
}

public sealed class FixtureDto
{
    public Guid Id { get; set; }
    public int Number { get; set; }                         // user-facing fixture number
    public string Name { get; set; } = string.Empty;
    public string Personality { get; set; } = string.Empty; // resolved from the FixtureLibrary
    public byte Universe { get; set; }
    public int Address { get; set; }                        // 1-based DMX address
}

public sealed class CueListDto
{
    public string Name { get; set; } = string.Empty;
    public List<CueDto> Cues { get; set; } = [];
}

public sealed class CueDto
{
    public int Major { get; set; }
    public int Minor { get; set; }
    public string Label { get; set; } = string.Empty;
    public double FadeSeconds { get; set; }
    public string Notes { get; set; } = string.Empty;
    public double? FollowSeconds { get; set; }          // null = no auto-follow
    public string? Type { get; set; }                   // null / "snapshot" | "chase" | "keys"
    public List<SnapshotDto> Fixtures { get; set; } = [];
    public ChaseDto? Chase { get; set; }                // present only on chase cues
    public KeysDto? Keys { get; set; }                  // present only on keyframed cues
}

public sealed class KeysDto
{
    public double DurationSeconds { get; set; }
    public bool Loop { get; set; }
    public List<TrackDto> Tracks { get; set; } = [];
}

public sealed class TrackDto
{
    public Guid FixtureId { get; set; }
    public int CapabilityIndex { get; set; }            // position within the fixture's personality
    public List<KeyDto> Keys { get; set; } = [];
}

public sealed class KeyDto
{
    public double TimeSeconds { get; set; }
    public byte[] Values { get; set; } = [];            // capability-shaped, as Capture() produces
    public string Interpolation { get; set; } = "linear";
}

public sealed class ChaseDto
{
    public double StepFadeSeconds { get; set; }
    public List<ChaseStepDto> Steps { get; set; } = [];
}

public sealed class ChaseStepDto
{
    public double DurationSeconds { get; set; }
    public List<SnapshotDto> Fixtures { get; set; } = [];
}

public sealed class SnapshotDto
{
    public Guid FixtureId { get; set; }
    public List<byte[]> Values { get; set; } = []; // one entry per capability, in capability order
}
