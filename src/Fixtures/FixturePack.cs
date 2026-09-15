using System.IO;
using System.IO.Compression;

namespace Fixtures;

// One fixture library, however it is stored. A pack is also the unit of composition: it maps
// exactly onto "a source of fixtures", so builtin, the user's own, and each installed plug-in
// library are all just packs.
//
// A fixture's path within the pack IS its identity ("robe/robin-600-ledwash"), so the file layout
// and the key space are the same thing and cannot drift apart.
public abstract class FixturePack : IDisposable
{
    public required PackManifest Manifest { get; init; }

    /// <summary>Where this pack came from, for diagnostics.</summary>
    public required string Location { get; init; }

    /// <summary>Fixture paths, '/'-separated and without the .xml suffix.</summary>
    public abstract IEnumerable<string> FixturePaths { get; }

    public abstract string ReadFixture(string fixturePath);

    public virtual void Dispose() => GC.SuppressFinalize(this);

    protected static string ToFixturePath(string relative) =>
        relative.Replace('\\', '/')[..^4]; // strip ".xml"
}

// A pack laid out as folders on disk. This is the editable form — the user's own library, and the
// built-ins as shipped, so that "copy one and edit it" is how someone writes their first fixture.
public sealed class DirectoryPack : FixturePack
{
    private readonly string _root;

    private DirectoryPack(string root) => _root = root;

    public static DirectoryPack Open(string root)
    {
        var manifestPath = Path.Combine(root, PackManifest.FileName);
        if (!File.Exists(manifestPath))
            throw new FixtureFormatException(root, 0, $"no {PackManifest.FileName} — not a fixture library");

        return new DirectoryPack(root)
        {
            Manifest = PackManifest.Parse(File.ReadAllText(manifestPath), manifestPath),
            Location = root
        };
    }

    public override IEnumerable<string> FixturePaths =>
        Directory.EnumerateFiles(_root, "*.xml", SearchOption.AllDirectories)
            .Select(p => Path.GetRelativePath(_root, p))
            .Where(p => !p.Equals(PackManifest.FileName, StringComparison.OrdinalIgnoreCase))
            .Select(ToFixturePath);

    public override string ReadFixture(string fixturePath) =>
        File.ReadAllText(Path.Combine(_root, fixturePath.Replace('/', Path.DirectorySeparatorChar) + ".xml"));
}

// A pack distributed as a single file (.lumfl, or a plain .zip). Read in place rather than
// extracted: nothing to leave stale on disk, and no zip-slip surface since we never write out.
public sealed class ZipPack : FixturePack
{
    // Packs get downloaded, so an untrusted one must not be able to exhaust memory. These are far
    // above any real library — QLC+'s whole collection is ~700 fixtures of a couple of KB each.
    private const int MaxEntries = 20_000;
    private const long MaxEntryBytes = 1024 * 1024;

    private readonly ZipArchive _archive;

    private ZipPack(ZipArchive archive) => _archive = archive;

    public static ZipPack Open(string path)
    {
        var archive = ZipFile.OpenRead(path);
        try
        {
            if (archive.Entries.Count > MaxEntries)
                throw new FixtureFormatException(path, 0, $"contains more than {MaxEntries} entries");

            var manifest = archive.GetEntry(PackManifest.FileName)
                ?? throw new FixtureFormatException(path, 0, $"no {PackManifest.FileName} — not a fixture library");

            return new ZipPack(archive)
            {
                Manifest = PackManifest.Parse(ReadEntry(manifest, path), $"{path}!{PackManifest.FileName}"),
                Location = path
            };
        }
        catch
        {
            archive.Dispose();
            throw;
        }
    }

    public override IEnumerable<string> FixturePaths =>
        _archive.Entries
            .Select(e => e.FullName)
            .Where(n => n.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            .Where(n => !n.Equals(PackManifest.FileName, StringComparison.OrdinalIgnoreCase))
            .Select(ToFixturePath);

    public override string ReadFixture(string fixturePath)
    {
        var entry = _archive.GetEntry(fixturePath + ".xml")
            ?? throw new FixtureFormatException(Location, 0, $"'{fixturePath}.xml' is not in the library");
        return ReadEntry(entry, Location);
    }

    private static string ReadEntry(ZipArchiveEntry entry, string source)
    {
        if (entry.Length > MaxEntryBytes)
            throw new FixtureFormatException(source, 0, $"'{entry.FullName}' is larger than {MaxEntryBytes / 1024}KB");

        // Read against the declared length rather than trusting the stream to end: a crafted
        // archive can understate Length, so the cap has to hold at read time too.
        using var stream = entry.Open();
        using var reader = new StreamReader(stream);
        var buffer = new char[MaxEntryBytes];
        var read = reader.ReadBlock(buffer, 0, buffer.Length);
        if (!reader.EndOfStream)
            throw new FixtureFormatException(source, 0, $"'{entry.FullName}' is larger than it declares");
        return new string(buffer, 0, read);
    }

    public override void Dispose()
    {
        _archive.Dispose();
        base.Dispose();
    }
}
