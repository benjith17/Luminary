using System.Collections.Generic;
using System.Linq;
using Fixtures;
using Model;

namespace ViewModel;

// Patched fixtures whose personality could not be resolved, grouped by the library they wanted.
public sealed class MissingPackGroup
{
    public required string PackId { get; init; }
    public required IReadOnlyList<string> Fixtures { get; init; }

    public string Heading => $"Fixture library '{PackId}' is not installed";
}

// What to tell the operator after loading a show: which patched fixtures will not output, and what
// went wrong reading the libraries. Both matter before the rig is trusted — a silently short patch
// is discovered on stage.
public sealed class FixtureIssuesViewModel : ViewModelBase
{
    public FixtureIssuesViewModel(IReadOnlyList<Fixture> missing, IReadOnlyList<LoadIssue> issues)
    {
        MissingPacks =
        [
            .. missing
                .GroupBy(f => f.FixtureType.PackId)
                .OrderBy(g => g.Key)
                .Select(g => new MissingPackGroup
                {
                    PackId = string.IsNullOrEmpty(g.Key) ? "(unknown)" : g.Key,
                    Fixtures = [.. g.OrderBy(f => f.Number)
                        .Select(f => $"#{f.Number}  {f.Name}  —  {f.FixtureType.Key}")]
                })
        ];

        Issues = [.. issues.Select(i => i.ToString())];
        MissingCount = missing.Count;
    }

    public IReadOnlyList<MissingPackGroup> MissingPacks { get; }
    public IReadOnlyList<string> Issues { get; }
    public int MissingCount { get; }

    public bool HasMissing => MissingCount > 0;
    public bool HasIssues => Issues.Count > 0;
    public bool HasAnything => HasMissing || HasIssues;

    public string Summary => MissingCount switch
    {
        0 => "There were problems loading the fixture libraries.",
        1 => "1 patched fixture uses a personality that is not installed.",
        _ => $"{MissingCount} patched fixtures use personalities that are not installed."
    };

    public string Reassurance =>
        "They keep their place in the patch and their addresses are preserved, so installing the " +
        "library will restore them. Until then they output nothing.";
}
