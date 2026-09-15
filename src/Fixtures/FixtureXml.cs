using System.Xml.Linq;
using Model;

namespace Fixtures;

// Parses one fixture document into its personalities. Pure: takes text, returns definitions or
// throws FixtureFormatException. No file system, no packs — those live in FixturePack.
public static class FixtureXml
{
    public static readonly XNamespace Ns = CapabilityRegistry.CoreNamespace;

    // Capabilities are grouped in the file by attribute family, in this order.
    private static readonly (string Element, CapabilityFamily Family)[] Families =
    [
        ("intensity", CapabilityFamily.Intensity),
        ("color",     CapabilityFamily.Color),
        ("focus",     CapabilityFamily.Focus),
        ("beam",      CapabilityFamily.Beam)
    ];

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
            var warnings = new List<string>();
            var capabilities = ReadCapabilities(mode, source, warnings);

            if (capabilities.Count == 0)
                throw new FixtureFormatException(source, CapabilityReader.Line(mode),
                    $"mode '{modeId}' declares no capabilities");

            // A capability past the declared footprint would write into whatever is patched next,
            // so this is an error rather than something to flag and carry on with.
            foreach (var capability in capabilities)
                foreach (var channel in capability.Channels)
                    if (channel >= channelCount)
                        throw new FixtureFormatException(source, CapabilityReader.Line(mode),
                            $"'{capability.Name}' uses channel {channel + 1}, beyond the {channelCount} " +
                            $"channels mode '{modeId}' declares");

            Check(capabilities, warnings);

            // Ordered by first channel, never by position in the file. Cues and keyframe tracks
            // address capabilities by index, so grouping a fixture into families must not reshuffle
            // them and silently repoint existing show data.
            capabilities.Sort((a, b) => a.Offset.CompareTo(b.Offset));

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
                Capabilities = capabilities,
                Warnings = warnings
            });
        }

        return definitions;
    }

    // Reads the family wrappers and everything inside them.
    private static List<FixtureCapability> ReadCapabilities(XElement mode, string source, List<string> warnings)
    {
        var capabilities = new List<FixtureCapability>();

        foreach (var child in mode.Elements())
        {
            var family = Families.FirstOrDefault(f => child.Name == Ns + f.Element);
            if (family.Element is null)
                throw new FixtureFormatException(source, CapabilityReader.Line(child),
                    $"<{child.Name.LocalName}> is not an attribute family — expected one of " +
                    string.Join(", ", Families.Select(f => $"<{f.Element}>")));

            var primaries = 0;
            foreach (var element in child.Elements())
            {
                var capability = BuildCapability(element, family.Family, source);

                if (capability.Primary && ++primaries > 1)
                {
                    // First primary wins; say so rather than silently picking one.
                    capability.Primary = false;
                    warnings.Add($"More than one capability in {family.Element} is marked primary; " +
                                 $"'{capability.Name}' is ignored.");
                }

                capabilities.Add(capability);
            }
        }

        return capabilities;
    }

    private static FixtureCapability BuildCapability(XElement element, CapabilityFamily family, string source)
    {
        if (!CapabilityRegistry.TryGet(element.Name, out var registration))
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

        if (!registration.AllowedIn(family))
            throw new FixtureFormatException(source, CapabilityReader.Line(element),
                $"<{element.Name.LocalName}> cannot appear in <{family.ToString().ToLowerInvariant()}>");

        var reader = new CapabilityReader(element, source);
        var capability = registration.Factory(reader);
        capability.Family = family;
        capability.Primary = reader.Bool("primary");
        if (reader.Fade() is { } fade) capability.Fade = fade;
        return capability;
    }

    // Things that do not stop a personality working but the operator should see before patching it.
    private static void Check(List<FixtureCapability> capabilities, List<string> warnings)
    {
        if (!capabilities.Any(c => c.Family == CapabilityFamily.Intensity))
            warnings.Add("No intensity capability: '@' with a single value will not resolve.");

        foreach (var group in capabilities.GroupBy(c => c.Name).Where(g => g.Count() > 1))
            warnings.Add($"{group.Count()} capabilities are named '{group.Key}', so 'set {group.Key}' " +
                         "depends on their order.");

        var seen = new Dictionary<int, string>();
        foreach (var capability in capabilities)
            foreach (var channel in capability.Channels)
                if (!seen.TryAdd(channel, capability.Name) && seen[channel] != capability.Name)
                    warnings.Add($"Channel {channel + 1} is used by both '{seen[channel]}' and " +
                                 $"'{capability.Name}'; output for it is unpredictable.");
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
