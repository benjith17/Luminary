using System.Xml.Linq;

namespace Fixtures;

// A fixture library's identity card, read from pack.xml at its root.
//
// Id comes from here rather than from the folder or file name on purpose: renaming
// "builtin.lumfl" must not re-identify every fixture inside it, because that id prefixes every
// key a saved show stores.
public sealed record PackManifest(
    string Id,
    string Name,
    int Version,
    string? Author,
    DateOnly? Created,
    IReadOnlyList<string> RequiredPlugins)
{
    public const string FileName = "pack.xml";

    public static readonly XNamespace Ns = "urn:luminary:pack:1";

    public static PackManifest Parse(string xml, string source)
    {
        XDocument doc;
        try
        {
            doc = XDocument.Parse(xml, LoadOptions.SetLineInfo);
        }
        catch (System.Xml.XmlException ex)
        {
            throw new FixtureFormatException(source, ex.LineNumber, ex.Message);
        }

        var root = doc.Root ?? throw new FixtureFormatException(source, 0, "the file is empty");
        if (root.Name != Ns + "pack")
            throw new FixtureFormatException(source, CapabilityReader.Line(root),
                $"expected a <pack> element in namespace '{Ns}', but found <{root.Name.LocalName}>");

        var id = root.Attribute("id")?.Value;
        if (string.IsNullOrWhiteSpace(id) || !IsSlug(id))
            throw new FixtureFormatException(source, CapabilityReader.Line(root),
                $"'id' must be a lowercase slug such as \"builtin\", but was '{id}'");

        var versionRaw = root.Element(Ns + "version")?.Value.Trim();
        if (!int.TryParse(versionRaw, out var version) || version < 1)
            throw new FixtureFormatException(source, CapabilityReader.Line(root),
                $"<version> must be a whole number of 1 or more, but was '{versionRaw}'");

        DateOnly? created = null;
        if (root.Element(Ns + "created")?.Value.Trim() is { Length: > 0 } createdRaw)
        {
            if (!DateOnly.TryParseExact(createdRaw, "yyyy-MM-dd", out var parsed))
                throw new FixtureFormatException(source, CapabilityReader.Line(root),
                    $"<created> must be a date as yyyy-MM-dd, but was '{createdRaw}'");
            created = parsed;
        }

        return new PackManifest(
            Id: id,
            Name: root.Element(Ns + "name")?.Value.Trim() is { Length: > 0 } n ? n : id,
            Version: version,
            Author: root.Element(Ns + "author")?.Value.Trim() is { Length: > 0 } a ? a : null,
            Created: created,
            RequiredPlugins: [.. root.Elements(Ns + "requires")
                .Select(e => e.Attribute("plugin")?.Value)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => v!)]);
    }

    private static bool IsSlug(string value) =>
        System.Text.RegularExpressions.Regex.IsMatch(value, "^[a-z0-9]+(-[a-z0-9]+)*$");
}
