using Fixtures;
using System.IO;
using System.Text.Json;
using Model;

namespace Persistence;

public sealed class JsonShowStore : IShowStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public string Serialize(ShowService show) =>
        JsonSerializer.Serialize(ToDto(show), Options);

    public void Save(ShowService show, string path) =>
        File.WriteAllText(path, Serialize(show));

    public ShowService Load(string path, FixtureLibrary library)
    {
        var dto = JsonSerializer.Deserialize<ShowFileDto>(File.ReadAllText(path), Options) ?? new ShowFileDto();
        return FromDto(dto, library);
    }

    private static ShowFileDto ToDto(ShowService show) => new()
    {
        Version = ShowFileDto.CurrentVersion,
        Universes = show.Universes.Select(u => new UniverseDto
        {
            Number = u.Number,
            Output = u.Output switch
            {
                ArtNetOutput a => new OutputDto { Type = "artnet", Ip = a.Ip, Port = a.Port, ArtNetUniverse = a.ArtNetUniverse },
                ArtNet4Output a => new OutputDto { Type = "artnet4", Port = a.Port, ArtNetUniverse = a.ArtNetUniverse, ManualTargets = a.ManualTargets },
                _ => null
            }
        }).ToList(),
        Fixtures = show.Fixtures.Select(f => new FixtureDto
        {
            Id = f.Id,
            Number = f.Number,
            Name = f.Name,
            Personality = f.FixtureType.Key,
            Universe = f.UniverseNumber,
            Address = f.Channel + 1
        }).ToList(),
        CueList = new CueListDto
        {
            Name = show.CueList.Name,
            Cues = show.CueList.Cues.Select(c => new CueDto
            {
                Major = c.CueMajor,
                Minor = c.CueMinor,
                Label = c.Label,
                FadeSeconds = c.FadeIn.TotalSeconds,
                Notes = c.Notes,
                FollowSeconds = c.Follow?.TotalSeconds,
                Type = c.Type switch { CueType.Chase => "chase", CueType.Keys => "keys", _ => null },
                Fixtures = ToSnapshotDtos(c.Fixtures),
                // Omitted entirely for snapshot cues, so their serialized form is unchanged.
                Chase = c.Type == CueType.Chase
                    ? new ChaseDto
                    {
                        StepFadeSeconds = c.Chase.StepFade.TotalSeconds,
                        Steps = c.Chase.Steps.Select(s => new ChaseStepDto
                        {
                            DurationSeconds = s.Duration.TotalSeconds,
                            Fixtures = ToSnapshotDtos(s.Fixtures)
                        }).ToList()
                    }
                    : null,
                Keys = c.Type == CueType.Keys
                    ? new KeysDto
                    {
                        DurationSeconds = c.Keys.Duration.TotalSeconds,
                        Loop = c.Keys.Loop,
                        Tracks = c.Keys.Tracks.Select(t => new TrackDto
                        {
                            FixtureId = t.FixtureId,
                            CapabilityIndex = t.CapabilityIndex,
                            Keys = t.Keys.Select(k => new KeyDto
                            {
                                TimeSeconds = k.Time.TotalSeconds,
                                Values = k.Values,
                                Interpolation = k.Interpolation.ToString().ToLowerInvariant()
                            }).ToList()
                        }).ToList()
                    }
                    : null
            }).ToList()
        },
        Macros = show.Macros.Select(m => new MacroDto { Name = m.Name, Source = m.Source }).ToList(),
        Bindings = show.Bindings.Select(b => new BindingDto
        {
            Name = b.Name,
            Action = b.Action,
            Trigger = new BindingTriggerDto
            {
                Kind = b.Trigger.Kind,
                Gesture = b.Trigger.Gesture,
                Device = b.Trigger.Device,
                Channel = b.Trigger.Channel,
                Message = b.Trigger.Message,
                Number = b.Trigger.Number,
                IgnoreZero = b.Trigger.IgnoreZero
            }
        }).ToList()
    };

    // Unknown values fall back to linear rather than throwing, so a file written by a newer build
    // still opens.
    private static Interpolation ParseInterpolation(string? name) =>
        Enum.TryParse<Interpolation>(name, ignoreCase: true, out var parsed) ? parsed : Interpolation.Linear;

    private static List<SnapshotDto> ToSnapshotDtos(List<CueFixtureSnapshot> snapshots) =>
        snapshots.Select(s => new SnapshotDto { FixtureId = s.FixtureId, Values = s.CapabilityValues }).ToList();

    private static List<CueFixtureSnapshot> FromSnapshotDtos(List<SnapshotDto> dtos) =>
        dtos.Select(s => new CueFixtureSnapshot { FixtureId = s.FixtureId, CapabilityValues = s.Values }).ToList();

    // Before version 4 a personality was stored as its display name, resolved against a hard-coded
    // library. Those names are now pack-qualified keys, so old shows are mapped across on load.
    // Anything not listed here was never a built-in, and becomes a missing-fixture placeholder.
    private static readonly Dictionary<string, string> LegacyPersonalityKeys = new()
    {
        ["Generic Dimmer"]  = "builtin:generic/dimmer/1-channel",
        ["Generic RGB"]     = "builtin:generic/rgb/3-channel",
        ["Big Test Light"]  = "builtin:generic/big-test-light/16-channel",
        ["Moving Head"]     = "builtin:generic/moving-head/6-channel",
        ["Encore Strobe"]   = "builtin:encore/strobe/34-channel",
        ["Robe Robin 600 LEDWash (Reduced RGBW Wash 8bit)"] = "builtin:robe/robin-600-ledwash/reduced-rgbw-wash-8bit",
    };

    private static string MigratePersonality(string stored, int version) =>
        version >= 4 ? stored : LegacyPersonalityKeys.GetValueOrDefault(stored, stored);

    private static ShowService FromDto(ShowFileDto dto, FixtureLibrary library)
    {
        var show = new ShowService
        {
            Universes = dto.Universes.Select(u => new Universe(u.Number)
            {
                Output = u.Output switch
                {
                    { Type: "artnet4" } o => new ArtNet4Output
                    {
                        Port = o.Port ?? 6454,
                        ArtNetUniverse = o.ArtNetUniverse ?? 0,
                        ManualTargets = o.ManualTargets ?? []
                    },
                    { Type: "artnet" } o => new ArtNetOutput
                    {
                        Ip = o.Ip ?? "255.255.255.255",
                        Port = o.Port ?? 6454,
                        ArtNetUniverse = o.ArtNetUniverse ?? 0
                    },
                    _ => (UniverseOutput?)null
                }
            }).ToList()
        };

        // Preserve stored fixture numbers; back-fill any that are missing (older files) with free ones.
        var usedNumbers = dto.Fixtures.Where(f => f.Number > 0).Select(f => f.Number).ToHashSet();
        int NextFreeNumber()
        {
            var n = 1;
            while (!usedNumbers.Add(n)) n++;
            return n;
        }

        foreach (var f in dto.Fixtures)
        {
            var key = MigratePersonality(f.Personality, dto.Version);

            // An unresolvable personality becomes a placeholder rather than being dropped. Dropping
            // it would silently shrink the patch, and the next save would write that loss to disk.
            var def = library.Get(key) ?? FixtureDefinition.Missing(key);

            show.Fixtures.Add(new Fixture(f.Name, f.Address - 1, def)
            {
                Id = f.Id,
                Number = f.Number > 0 ? f.Number : NextFreeNumber(),
                UniverseNumber = f.Universe
            });
        }

        show.CueList = new CueList
        {
            Name = dto.CueList.Name,
            Cues = dto.CueList.Cues.Select(c => new Cue
            {
                CueMajor = c.Major,
                CueMinor = c.Minor,
                Label = c.Label,
                FadeIn = TimeSpan.FromSeconds(c.FadeSeconds),
                Notes = c.Notes,
                Follow = c.FollowSeconds is { } s ? TimeSpan.FromSeconds(s) : null,
                Type = c.Type switch { "chase" => CueType.Chase, "keys" => CueType.Keys, _ => CueType.Snapshot },
                Fixtures = FromSnapshotDtos(c.Fixtures),
                Chase = c.Chase is { } chase
                    ? new Chase
                    {
                        StepFade = TimeSpan.FromSeconds(chase.StepFadeSeconds),
                        Steps = chase.Steps.Select(s => new ChaseStep
                        {
                            Duration = TimeSpan.FromSeconds(s.DurationSeconds),
                            Fixtures = FromSnapshotDtos(s.Fixtures)
                        }).ToList()
                    }
                    : new Chase(),
                Keys = c.Keys is { } keys
                    ? new KeyframeSequence
                    {
                        Duration = TimeSpan.FromSeconds(keys.DurationSeconds),
                        Loop = keys.Loop,
                        Tracks = keys.Tracks.Select(t => new KeyframeTrack
                        {
                            FixtureId = t.FixtureId,
                            CapabilityIndex = t.CapabilityIndex,
                            // Sorted on load: playback binary-searches the list, and a hand-edited
                            // file has no other guarantee of order.
                            Keys = t.Keys.OrderBy(k => k.TimeSeconds).Select(k => new Keyframe
                            {
                                Time = TimeSpan.FromSeconds(k.TimeSeconds),
                                Values = k.Values,
                                Interpolation = ParseInterpolation(k.Interpolation)
                            }).ToList()
                        }).ToList()
                    }
                    : new KeyframeSequence()
            }).ToList()
        };

        show.Macros = dto.Macros.Select(m => new Macro { Name = m.Name, Source = m.Source }).ToList();

        show.Bindings = dto.Bindings.Select(b => new Binding
        {
            Name = b.Name,
            Action = b.Action,
            Trigger = new BindingTrigger
            {
                Kind = b.Trigger.Kind,
                Gesture = b.Trigger.Gesture,
                Device = b.Trigger.Device,
                Channel = b.Trigger.Channel,
                Message = b.Trigger.Message,
                Number = b.Trigger.Number,
                IgnoreZero = b.Trigger.IgnoreZero
            }
        }).ToList();

        return show;
    }
}
