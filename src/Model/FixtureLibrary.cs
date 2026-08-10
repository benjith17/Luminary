namespace Model;

// The set of fixture personalities available for patching. For now a fixed built-in set;
// this is the seam where externally-provided (DLL) personalities will later be merged in.
public class FixtureLibrary
{
    public IReadOnlyList<FixtureDefinition> Definitions { get; } = BuildBuiltIns();

    public FixtureDefinition? Get(string name) =>
        Definitions.FirstOrDefault(d => d.Name == name);

    private static List<FixtureDefinition> BuildBuiltIns() =>
    [
        new FixtureDefinition
        {
            Name = "Generic Dimmer",
            ChannelCount = 1,
            Capabilities = [new DimmerCapability("Dimmer", offset: 0)]
        },
        new FixtureDefinition
        {
            Name = "Generic RGB",
            ChannelCount = 3,
            Capabilities = [new ColorCapability("Color", redOffset: 0, greenOffset: 1, blueOffset: 2)]
        },
        new FixtureDefinition
        {
            Name = "Big Test Light",
            ChannelCount = 16,
            Capabilities =
            [
                new DimmerCapability("Dimmer", offset: 0),
                new ColorCapability("Color", redOffset: 1, greenOffset: 2, blueOffset: 3),
                new ColorCapability("Color", redOffset: 4, greenOffset: 5, blueOffset: 6),
                new ColorCapability("Color", redOffset: 7, greenOffset: 8, blueOffset: 9),
                new ColorCapability("Color", redOffset: 10, greenOffset: 11, blueOffset: 12),
                new ColorCapability("Color", redOffset: 13, greenOffset: 14, blueOffset: 15),
            ]
        },
        new FixtureDefinition
        {
            Name = "Moving Head",
            ChannelCount = 6,
            Capabilities =
            [
                new PanTiltCapability("Pan/Tilt", panOffset: 0, tiltOffset: 1),
                new DimmerCapability("Dimmer", offset: 2),
                new ColorCapability("Color", redOffset: 3, greenOffset: 4, blueOffset: 5),
            ]
        },
        new FixtureDefinition
        {
            Name = "Encore Strobe",
            ChannelCount = 34,
            Capabilities =
            [
                new DimmerCapability("Strobe", offset: 0),
                new DimmerFineCapability("Dimmer", offset: 1, fineOffset: 2),
                new ColorCapability("Color", redOffset: 4, greenOffset: 5, blueOffset: 3),
                new DimmerCapability("CTO", offset: 6),
                new PanTiltFineCapability("Pan/Tilt",
                    panOffset: 28, panFineOffset: 29, tiltOffset: 30, tiltFineOffset: 31,
                    defaultPan: 32768, defaultTilt: 32768),
                new DimmerCapability("Effect", offset: 33),
            ]
        },
    ];
}
