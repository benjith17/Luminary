using System.Xml;
using System.Xml.Linq;
using Model;

namespace Fixtures;

// Typed, error-reporting access to a capability element's attributes.
//
// This is the ONLY place file channel numbers become internal offsets. Files are 1-based, matching
// how manufacturer manuals number channels ("Pan is channel 1"); FixtureCapability is 0-based.
// Keeping the conversion here means no capability factory has to remember it.
public readonly struct CapabilityReader(XElement element, string source)
{
    public XElement Element => element;

    public string Name() => Required("name");

    /// <summary>A 1-based channel from the file, returned as a 0-based offset.</summary>
    public int Channel(string attribute)
    {
        var raw = Required(attribute);
        if (!int.TryParse(raw, out var channel) || channel < 1)
            throw Error($"'{attribute}' must be a channel number of 1 or more, but was '{raw}'");
        return channel - 1;
    }

    public byte Byte(string attribute, byte fallback = 0)
    {
        if (element.Attribute(attribute)?.Value is not { } raw) return fallback;
        if (!byte.TryParse(raw, out var value))
            throw Error($"'{attribute}' must be 0-255, but was '{raw}'");
        return value;
    }

    public ushort Word(string attribute, ushort fallback = 0)
    {
        if (element.Attribute(attribute)?.Value is not { } raw) return fallback;
        if (!ushort.TryParse(raw, out var value))
            throw Error($"'{attribute}' must be 0-65535, but was '{raw}'");
        return value;
    }

    public bool Bool(string attribute, bool fallback = false)
    {
        if (element.Attribute(attribute)?.Value is not { } raw) return fallback;
        if (!bool.TryParse(raw, out var value))
            throw Error($"'{attribute}' must be true or false, but was '{raw}'");
        return value;
    }

    public FadeBehavior? Fade()
    {
        if (element.Attribute("fade")?.Value is not { } raw) return null;
        return raw.ToLowerInvariant() switch
        {
            "fade" => FadeBehavior.Fade,
            "snap" => FadeBehavior.Snap,
            _ => throw Error($"'fade' must be fade or snap, but was '{raw}'")
        };
    }

    public BlackoutBehavior? Blackout()
    {
        if (element.Attribute("blackout")?.Value is not { } raw) return null;
        return raw.ToLowerInvariant() switch
        {
            "zero" => BlackoutBehavior.Zero,
            "hold" => BlackoutBehavior.Hold,
            _ => throw Error($"'blackout' must be zero or hold, but was '{raw}'")
        };
    }

    private string Required(string attribute) =>
        element.Attribute(attribute)?.Value
        ?? throw Error($"<{element.Name.LocalName}> is missing the required '{attribute}' attribute");

    private FixtureFormatException Error(string message) =>
        new(source, Line(element), message);

    internal static int Line(XObject node) =>
        node is IXmlLineInfo info && info.HasLineInfo() ? info.LineNumber : 0;
}

// A fixture or manifest file that could not be understood. Carries enough to point the user at the
// exact spot — these files are hand-authored, so "which line" is most of the answer.
public sealed class FixtureFormatException(string source, int line, string message)
    : Exception(line > 0 ? $"{source}:{line}: {message}" : $"{source}: {message}")
{
    // Not "Source" — Exception already defines that.
    public string SourceName { get; } = source;
    public int Line { get; } = line;
    public string Detail { get; } = message;
}
