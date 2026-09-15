using Model;

namespace Fixtures;

public enum IssueSeverity { Warning, Error }

// Something the user needs told about their fixture libraries. These are hand-authored files from
// several places, so loading is expected to have things to say — and saying nothing is how a
// mistyped fixture silently becomes a missing one.
public sealed record LoadIssue(IssueSeverity Severity, string Message, string? Location = null)
{
    public override string ToString() => Location is null ? Message : $"{Message}  ({Location})";
}

// Every personality available for patching, composed from all loaded packs.
//
// Lookup is by the full pack-qualified key only. There is deliberately no fallback to an
// unqualified match: a show that names builtin:robe/... must never silently resolve to a
// different pack's fixture of the same path.
public class FixtureLibrary
{
    private readonly Dictionary<string, FixtureDefinition> _byKey;

    public FixtureLibrary(IReadOnlyList<FixtureDefinition> definitions, IReadOnlyList<LoadIssue> issues)
    {
        Definitions = definitions;
        Issues = issues;
        _byKey = definitions.ToDictionary(d => d.Key);
    }

    /// <summary>An empty library — used by the designer and before any load has happened.</summary>
    public FixtureLibrary() : this([], []) { }

    public IReadOnlyList<FixtureDefinition> Definitions { get; }

    /// <summary>Problems found while loading. Surfaced to the user rather than swallowed.</summary>
    public IReadOnlyList<LoadIssue> Issues { get; }

    public bool HasErrors => Issues.Any(i => i.Severity == IssueSeverity.Error);

    public FixtureDefinition? Get(string key) => _byKey.GetValueOrDefault(key);
}
