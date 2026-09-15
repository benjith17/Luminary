using System.Xml.Linq;
using Model;

namespace Fixtures;

/// <summary>
/// Builds one <see cref="FixtureCapability"/> from its XML element.
/// </summary>
public delegate FixtureCapability CapabilityFactory(CapabilityReader reader);

/// <summary>
/// How to build a capability, and which families it may appear in. A null
/// <paramref name="Families"/> means any — that is how generic levels and plug-in capabilities
/// are registered.
/// </summary>
public sealed record CapabilityRegistration(CapabilityFactory Factory, IReadOnlySet<CapabilityFamily>? Families)
{
    public bool AllowedIn(CapabilityFamily family) => Families is null || Families.Contains(family);
}

// Maps a capability element to the code that builds it, keyed on the FULL XML name — namespace
// plus local name, not the local name alone. That is what lets two plug-ins each define a
// "goboWheel" without colliding: they declare different namespaces, so they are different keys.
//
// This is the one place a register-function pattern genuinely belongs. The set is small and
// closed for core capabilities, and a plug-in assembly registers its own on load — at which
// point the assembly is definitely loaded, so the registration definitely runs.
public static class CapabilityRegistry
{
    public const string CoreNamespace = "urn:luminary:fixture:1";

    private static readonly Dictionary<XName, CapabilityRegistration> Registrations = [];

    static CapabilityRegistry() => RegisterCore();

    /// <summary>
    /// Registers a capability element. Omit <paramref name="families"/> to allow it anywhere —
    /// the right choice for a plug-in capability unless it is meaningless outside one family.
    /// </summary>
    public static void Register(XName element, CapabilityFactory factory, params CapabilityFamily[] families) =>
        Registrations[element] = new CapabilityRegistration(
            factory, families.Length == 0 ? null : families.ToHashSet());

    public static bool TryGet(XName element, out CapabilityRegistration registration) =>
        Registrations.TryGetValue(element, out registration!);

    /// <summary>Namespaces with at least one registered capability — used to report what a pack needs.</summary>
    public static IReadOnlySet<string> KnownNamespaces =>
        Registrations.Keys.Select(n => n.NamespaceName).ToHashSet();

    // Attribute names mirror the constructor parameters of each capability, minus the "Offset"
    // suffix. CapabilityReader converts 1-based file channels to the 0-based offsets used here.
    private static void RegisterCore()
    {
        XName Core(string local) => XName.Get(local, CoreNamespace);

        // Intensity only: a dimmer is the fixture's intensity, and nothing else is.
        Register(Core("dimmer"), r => new DimmerCapability(
            r.Name(), r.Channel("channel"), r.Byte("default")), CapabilityFamily.Intensity);

        Register(Core("dimmerFine"), r => new DimmerFineCapability(
            r.Name(), r.Channel("channel"), r.Channel("fine"), r.Word("default")), CapabilityFamily.Intensity);

        Register(Core("rgb"), r => new ColorCapability(
            r.Name(), r.Channel("red"), r.Channel("green"), r.Channel("blue"),
            r.Byte("defaultRed"), r.Byte("defaultGreen"), r.Byte("defaultBlue")), CapabilityFamily.Color);

        Register(Core("panTilt"), r => new PanTiltCapability(
            r.Name(), r.Channel("pan"), r.Channel("tilt"),
            r.Byte("defaultPan"), r.Byte("defaultTilt")), CapabilityFamily.Focus);

        Register(Core("panTiltFine"), r => new PanTiltFineCapability(
            r.Name(), r.Channel("pan"), r.Channel("panFine"), r.Channel("tilt"), r.Channel("tiltFine"),
            r.Word("defaultPan"), r.Word("defaultTilt")), CapabilityFamily.Focus);

        // Generic single channels, valid in any family.
        Register(Core("level"), r => new LevelCapability(
            r.Name(), r.Channel("channel"), r.Byte("default")));

        Register(Core("levelFine"), r => new LevelFineCapability(
            r.Name(), r.Channel("channel"), r.Channel("fine"), r.Word("default")));
    }
}
