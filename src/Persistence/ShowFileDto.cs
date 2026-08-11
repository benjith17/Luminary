namespace Persistence;

// The serialized shape of a show file. Kept separate from the runtime model so the file is a
// stable contract: no live channel data, personalities referenced by name, outputs discriminated
// by type, fixtures referenced by stable id.

public sealed class ShowFileDto
{
    public int Version { get; set; } = 1;
    public List<UniverseDto> Universes { get; set; } = [];
    public List<FixtureDto> Fixtures { get; set; } = [];
    public CueListDto CueList { get; set; } = new();
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
    public List<SnapshotDto> Fixtures { get; set; } = [];
}

public sealed class SnapshotDto
{
    public Guid FixtureId { get; set; }
    public List<byte[]> Values { get; set; } = []; // one entry per capability, in capability order
}
