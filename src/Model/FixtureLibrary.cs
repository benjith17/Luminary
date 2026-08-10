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
            Manufacturer = "Generic", Model = "Dimmer", Mode = "1 Channel",
            ChannelCount = 1,
            Capabilities = [new DimmerCapability("Dimmer", offset: 0)]
        },
        new FixtureDefinition
        {
            Name = "Generic RGB",
            Manufacturer = "Generic", Model = "RGB", Mode = "3 Channel",
            ChannelCount = 3,
            Capabilities = [new ColorCapability("Color", redOffset: 0, greenOffset: 1, blueOffset: 2)]
        },
        new FixtureDefinition
        {
            Name = "Big Test Light",
            Manufacturer = "Generic", Model = "Big Test Light", Mode = "16 Channel",
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
            Manufacturer = "Generic", Model = "Moving Head", Mode = "6 Channel",
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
            Manufacturer = "Encore", Model = "Strobe", Mode = "34 Channel",
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
        new FixtureDefinition
        {
            Name = "Robe Robin 600 LEDWash (Reduced RGBW Wash 8bit)",
            Manufacturer = "Robe", Model = "Robin 600 LEDWash", Mode = "Reduced RGBW Wash 8bit",
            ChannelCount = 15,
            Capabilities =
            [
                new PanTiltFineCapability("Pan/Tilt",
                    panOffset: 0, panFineOffset: 1, tiltOffset: 2, tiltFineOffset: 3,
                    defaultPan: 32768, defaultTilt: 32768),
                new DimmerCapability("PositionMSpeed", offset: 4),
                new DimmerCapability("Control 1", offset: 5),
                new ColorCapability("Color", redOffset: 6, greenOffset: 7, blueOffset: 8),
                new DimmerCapability("White", offset: 9),
                new DimmerCapability("CTO", offset: 10),
                new DimmerCapability("Color", offset: 11),
                new DimmerCapability("Zoom", offset: 12),
                new DimmerCapability("Shutter", offset: 13),
                new DimmerCapability("Dimmer", offset: 14),
            ]
        }
    ];
}
