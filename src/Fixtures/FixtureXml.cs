using System.Xml.Linq;
using Model;

namespace Fixtures;

// Parses one fixture document into its personalities. Pure: takes text, returns definitions or
// throws FixtureFormatException. No file system, no packs — those live in FixturePack.
public static class FixtureXml
{
    public static readonly XNamespace Ns = CapabilityRegistry.CoreNamespace;

    /// <summary>
    /// Reads a fixture file. <paramref name="packId"/> and <paramref name="fixturePath"/> supply the
    /// identity the document deliberately does not carry: a fixture is identified by where it sits,
    /// so "builtin" + "robe/robin-600-ledwash" + a mode id form the key a show stores.
    /// </summary>
    public static List<FixtureDefinition> Parse(string xml, string packId, string fixturePath, string source)
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
        if (root.Name != Ns + "fixture")
            throw new FixtureFormatException(source, CapabilityReader.Line(root),
                $"expected a <fixture> element in namespace '{Ns}', but found <{root.Name.LocalName}>");

        var manufacturer = Text(root, "manufacturer", source);
        var model = Text(root, "model", source);
        var author = root.Element(Ns + "author")?.Value.Trim();
        var created = Date(root, source);

        var modes = root.Elements(Ns + "mode").ToList();
        if (modes.Count == 0)
            throw new FixtureFormatException(source, CapabilityReader.Line(root),
                "a fixture must define at least one <mode>");

        var definitions = new List<FixtureDefinition>();
        var seenModeIds = new HashSet<string>();

        foreach (var mode in modes)
        {
            var modeId = Attribute(mode, "id", source);
            if (!seenModeIds.Add(modeId))
                throw new FixtureFormatException(source, CapabilityReader.Line(mode),
                    $"duplicate mode id '{modeId}' — mode ids must be unique within a fixture");

            var channelCount = ChannelCount(mode, source);
            var capabilities = mode.Elements()
                .Select(e => BuildCapability(e, source))
                .ToList();

            if (capabilities.Count == 0)
                throw new FixtureFormatException(source, CapabilityReader.Line(mode),
                    $"mode '{modeId}' declares no capabilities");

            definitions.Add(new FixtureDefinition
            {
                Key = $"{packId}:{fixturePath}/{modeId}",
                PackId = packId,
                FixturePath = fixturePath,
                ModeId = modeId,
                Manufacturer = manufacturer,
                Model = model,
                Mode = Attribute(mode, "name", source),
                Author = string.IsNullOrWhiteSpace(author) ? null : author,
                Created = created,
                ChannelCount = channelCount,
                Capabilities = capabilities
            });
        }

        return definitions;
    }

    private static FixtureCapability BuildCapability(XElement element, string source)
    {
        if (!CapabilityRegistry.TryGet(element.Name, out var factory))
        {
            // An unrecognised capability leaves the personality with an incomplete channel map, so
            // it is an error rather than something to skip — a fixture missing a channel is worse
            // than a fixture that refuses to load.
            var hint = element.Name.NamespaceName == CapabilityRegistry.CoreNamespace
                ? "is not a known capability type"
                : $"needs a plug-in providing namespace '{element.Name.NamespaceName}'";
            throw new FixtureFormatException(source, CapabilityReader.Line(element),
                $"<{element.Name.LocalName}> {hint}");
        }

        return factory(new CapabilityReader(element, source));
    }

    private static string Text(XElement root, string name, string source) =>
        root.Element(Ns + name)?.Value.Trim() is { Length: > 0 } value
            ? value
            : throw new FixtureFormatException(source, CapabilityReader.Line(root),
                $"<{name}> is required and must not be empty");

    private static string Attribute(XElement element, string name, string source) =>
        element.Attribute(name)?.Value is { Length: > 0 } value
            ? value
            : throw new FixtureFormatException(source, CapabilityReader.Line(element),
                $"<{element.Name.LocalName}> is missing the required '{name}' attribute");

    private static int ChannelCount(XElement mode, string source)
    {
        var raw = Attribute(mode, "channelCount", source);
        if (!int.TryParse(raw, out var count) || count < 1 || count > 512)
            throw new FixtureFormatException(source, CapabilityReader.Line(mode),
                $"'channelCount' must be between 1 and 512, but was '{raw}'");
        return count;
    }

    private static DateOnly? Date(XElement root, string source)
    {
        if (root.Element(Ns + "created")?.Value.Trim() is not { Length: > 0 } raw) return null;
        if (!DateOnly.TryParseExact(raw, "yyyy-MM-dd", out var date))
            throw new FixtureFormatException(source, CapabilityReader.Line(root),
                $"<created> must be a date as yyyy-MM-dd, but was '{raw}'");
        return date;
    }
}
