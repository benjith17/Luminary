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
        Version = 1,
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
            Name = f.Name,
            Personality = f.FixtureType.Name,
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
                Fixtures = c.Fixtures.Select(s => new SnapshotDto
                {
                    FixtureId = s.FixtureId,
                    Values = s.CapabilityValues
                }).ToList()
            }).ToList()
        }
    };

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

        foreach (var f in dto.Fixtures)
        {
            // Personality may be unavailable (e.g. a plug-in that isn't installed) — skip it.
            if (library.Get(f.Personality) is not { } def) continue;

            show.Fixtures.Add(new Fixture(f.Name, f.Address - 1, def)
            {
                Id = f.Id,
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
                Fixtures = c.Fixtures.Select(s => new CueFixtureSnapshot
                {
                    FixtureId = s.FixtureId,
                    CapabilityValues = s.Values
                }).ToList()
            }).ToList()
        };

        return show;
    }
}
