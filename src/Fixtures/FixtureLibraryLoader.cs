using System.IO;
using Model;

namespace Fixtures;

// Finds fixture libraries, loads them, and composes them into one FixtureLibrary.
//
// Three places are searched, in this order:
//
//   1. <app dir>/Fixtures          — ships with Luminary; read-only by convention
//   2. %AppData%/Luminary/Fixtures — the user's own libraries and installed packs
//   3. alongside the open .lum     — .lumfl files ONLY
//
// The show folder is restricted to .lumfl so that opening a show never means treating arbitrary
// neighbouring folders and zips as libraries. It exists so a show can travel with the fixtures it
// needs.
//
// Order affects nothing but the order of Definitions: lookup is by full pack-qualified key, so no
// pack can shadow another. Two packs claiming the same id is an error, not a precedence question.
public static class FixtureLibraryLoader
{
    public const string PackExtension = ".lumfl";
    private const string FixturesFolder = "Fixtures";

    public static FixtureLibrary Load(string? showDirectory = null)
    {
        var definitions = new List<FixtureDefinition>();
        var issues = new List<LoadIssue>();
        var packs = new List<FixturePack>();

        try
        {
            // Pass one: open every candidate and read its manifest. Nothing is loaded yet, because
            // whether a pack is usable depends on what the other packs turn out to be.
            foreach (var (location, packFilesOnly) in SearchLocations(showDirectory))
            foreach (var candidate in Candidates(location, packFilesOnly, issues).OrderBy(c => c, StringComparer.Ordinal))
            {
                try
                {
                    packs.Add(Open(candidate));
                }
                catch (FixtureFormatException ex)
                {
                    issues.Add(Issue(ex));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
                {
                    issues.Add(new LoadIssue(IssueSeverity.Error,
                        $"Could not read fixture library: {ex.Message}", candidate));
                }
            }

            // Pass two: load the packs whose identity is unambiguous.
            foreach (var group in packs.GroupBy(p => p.Manifest.Id).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                var versions = group.Select(p => p.Manifest.Version).Distinct().ToList();

                // Same id at two different versions: load NEITHER. Picking one arbitrarily would send
                // a show's fixtures the channel map of a version it was not built against, and wrong
                // DMX reaching real lights is worse than none. The user has to resolve it.
                if (versions.Count > 1)
                {
                    issues.Add(new LoadIssue(IssueSeverity.Error,
                        $"Fixture library '{group.Key}' is installed at {versions.Count} different versions " +
                        $"(v{string.Join(", v", versions.Order())}). Remove all but one — its fixtures are " +
                        "unavailable until you do: " + string.Join("; ", group.Select(p => p.Location))));
                    continue;
                }

                // Same id AND same version is the benign case: a show travelling with a library the
                // user already has installed. Deduplicate it silently.
                LoadPack(group.First(), definitions, issues);
            }
        }
        finally
        {
            foreach (var pack in packs) pack.Dispose();
        }

        // Finding nothing at all means a broken install or a stray working directory. Without this
        // the symptom is an empty fixture picker and no reason given.
        if (packs.Count == 0)
            issues.Add(new LoadIssue(IssueSeverity.Error,
                "No fixture libraries were found. Luminary looked in: " +
                string.Join("; ", SearchLocations(showDirectory).Select(l => l.Path))));

        // Stable, alphabetical order so the picker does not depend on directory enumeration.
        definitions.Sort((a, b) =>
        {
            var c = string.Compare(a.Manufacturer, b.Manufacturer, StringComparison.OrdinalIgnoreCase);
            if (c != 0) return c;
            c = string.Compare(a.Model, b.Model, StringComparison.OrdinalIgnoreCase);
            return c != 0 ? c : string.Compare(a.Mode, b.Mode, StringComparison.OrdinalIgnoreCase);
        });

        return new FixtureLibrary(definitions, issues);
    }

    private static LoadIssue Issue(FixtureFormatException ex) =>
        new(IssueSeverity.Error, ex.Detail, ex.SourceName + (ex.Line > 0 ? $":{ex.Line}" : string.Empty));

    private static void LoadPack(FixturePack pack, List<FixtureDefinition> definitions, List<LoadIssue> issues)
    {
        // Checked before any fixture is parsed, so a pack needing an uninstalled plug-in fails once
        // and clearly, instead of part-way through with an unknown-element error per fixture.
        var known = CapabilityRegistry.KnownNamespaces;
        var missing = pack.Manifest.RequiredPlugins.Where(p => !known.Contains(p)).ToList();
        if (missing.Count > 0)
        {
            issues.Add(new LoadIssue(IssueSeverity.Error,
                $"Fixture library '{pack.Manifest.Id}' needs plug-ins that are not installed: " +
                string.Join(", ", missing), pack.Location));
            return;
        }

        foreach (var fixturePath in pack.FixturePaths.OrderBy(p => p, StringComparer.Ordinal))
        {
            var source = $"{pack.Location}/{fixturePath}.xml";
            try
            {
                definitions.AddRange(
                    FixtureXml.Parse(pack.ReadFixture(fixturePath), pack.Manifest.Id, fixturePath, source));
            }
            catch (FixtureFormatException ex)
            {
                // One bad fixture must not cost the user the rest of the library.
                issues.Add(Issue(ex));
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException)
            {
                issues.Add(new LoadIssue(IssueSeverity.Error, $"Could not read fixture: {ex.Message}", source));
            }
        }
    }

    private static FixturePack Open(string path) =>
        Directory.Exists(path) ? DirectoryPack.Open(path) : ZipPack.Open(path);

    // (location, showFolderOnly) — the flag restricts a location to single-file packs.
    private static IEnumerable<(string Path, bool PackFilesOnly)> SearchLocations(string? showDirectory)
    {
        yield return (Path.Combine(AppContext.BaseDirectory, FixturesFolder), false);
        yield return (Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Luminary", FixturesFolder), false);

        if (!string.IsNullOrWhiteSpace(showDirectory))
            yield return (showDirectory, true);
    }

    private static IEnumerable<string> Candidates(string location, bool packFilesOnly, List<LoadIssue> issues)
    {
        if (!Directory.Exists(location)) yield break;

        string[] files;
        string[] directories;
        try
        {
            files = Directory.GetFiles(location);
            directories = packFilesOnly ? [] : Directory.GetDirectories(location);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            issues.Add(new LoadIssue(IssueSeverity.Warning,
                $"Could not search for fixture libraries: {ex.Message}", location));
            yield break;
        }

        foreach (var file in files)
        {
            var extension = Path.GetExtension(file);
            var isPackFile = extension.Equals(PackExtension, StringComparison.OrdinalIgnoreCase);
            var isZip = !packFilesOnly && extension.Equals(".zip", StringComparison.OrdinalIgnoreCase);
            if (isPackFile || isZip) yield return file;
        }

        foreach (var directory in directories)
            if (File.Exists(Path.Combine(directory, PackManifest.FileName)))
                yield return directory;
    }
}
