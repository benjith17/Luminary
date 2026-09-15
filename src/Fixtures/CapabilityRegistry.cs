using System.Xml.Linq;
using Model;

namespace Fixtures;

/// <summary>
/// Builds one <see cref="FixtureCapability"/> from its XML element.
/// </summary>
public delegate FixtureCapability CapabilityFactory(CapabilityReader reader);

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

    private static readonly Dictionary<XName, CapabilityFactory> Factories = [];

    static CapabilityRegistry() => RegisterCore();

    public static void Register(XName element, CapabilityFactory factory) => Factories[element] = factory;

    public static bool TryGet(XName element, out CapabilityFactory factory) =>
        Factories.TryGetValue(element, out factory!);

    /// <summary>Namespaces with at least one registered capability — used to report what a pack needs.</summary>
    public static IReadOnlySet<string> KnownNamespaces =>
        Factories.Keys.Select(n => n.NamespaceName).ToHashSet();

    // Attribute names mirror the constructor parameters of each capability, minus the "Offset"
    // suffix. CapabilityReader converts 1-based file channels to the 0-based offsets used here.
    private static void RegisterCore()
    {
        XName Core(string local) => XName.Get(local, CoreNamespace);

        Register(Core("dimmer"), r => new DimmerCapability(
            r.Name(), r.Channel("channel"), r.Byte("default")));

        Register(Core("dimmerFine"), r => new DimmerFineCapability(
            r.Name(), r.Channel("channel"), r.Channel("fine"), r.Word("default")));

        Register(Core("color"), r => new ColorCapability(
            r.Name(), r.Channel("red"), r.Channel("green"), r.Channel("blue"),
            r.Byte("defaultRed"), r.Byte("defaultGreen"), r.Byte("defaultBlue")));

        Register(Core("panTilt"), r => new PanTiltCapability(
            r.Name(), r.Channel("pan"), r.Channel("tilt"),
            r.Byte("defaultPan"), r.Byte("defaultTilt")));

        Register(Core("panTiltFine"), r => new PanTiltFineCapability(
            r.Name(), r.Channel("pan"), r.Channel("panFine"), r.Channel("tilt"), r.Channel("tiltFine"),
            r.Word("defaultPan"), r.Word("defaultTilt")));
    }
}
