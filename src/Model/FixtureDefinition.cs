namespace Model;

// One patchable personality: a single <mode> of a fixture file.
//
// Identity is split deliberately. Key is what a saved show stores and is derived from where the
// file sits in its pack; the display fields below are free to be corrected without orphaning a
// show. See FixtureXml for how a Key is assembled.
public class FixtureDefinition
{
    // Fully-qualified, pack-scoped stable key: "pack:fixture/path/mode-id".
    // e.g. "builtin:robe/robin-600-ledwash/reduced-rgbw-wash-8bit"
    public string Key { get; set; } = string.Empty;

    // The pack this came from, and the fixture's path within it — the two halves of Key, kept
    // separately so diagnostics can name the pack and locate the file.
    public string PackId { get; set; } = string.Empty;
    public string FixturePath { get; set; } = string.Empty;
    public string ModeId { get; set; } = string.Empty;

    // Hierarchy for the fixture picker: Manufacturer → Model → Mode (personality). Display only.
    public string Manufacturer { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Mode { get; set; } = string.Empty;

    // Provenance of the definition itself (not of the light). Both optional in the file.
    public string? Author { get; set; }
    public DateOnly? Created { get; set; }

    // Number of DMX channels this personality occupies, used for patching / addressing.
    public int ChannelCount { get; set; }

    public List<FixtureCapability> Capabilities { get; set; } = [];

    // True for a placeholder standing in for a personality this installation cannot resolve —
    // a show referencing a library that isn't installed. It carries no capabilities, so it emits
    // no DMX, but it keeps the Key so saving the show preserves the patch instead of erasing it.
    public bool IsMissing { get; init; }

    // Display label, used by the picker and anywhere a fixture needs naming in the UI.
    public string Name => IsMissing ? $"Missing: {Key}" : $"{Manufacturer} {Model} ({Mode})";

    /// <summary>
    /// A stand-in for an unresolvable personality. Without this, loading a show that references an
    /// uninstalled library would drop the fixture — and the next save would make that permanent.
    /// </summary>
    public static FixtureDefinition Missing(string key)
    {
        // "pack:fixture/path/mode-id" — salvage what we can for display.
        var packSplit = key.IndexOf(':');
        var pack = packSplit > 0 ? key[..packSplit] : string.Empty;
        var rest = packSplit > 0 ? key[(packSplit + 1)..] : key;
        var modeSplit = rest.LastIndexOf('/');

        return new FixtureDefinition
        {
            Key = key,
            PackId = pack,
            FixturePath = modeSplit > 0 ? rest[..modeSplit] : rest,
            ModeId = modeSplit > 0 ? rest[(modeSplit + 1)..] : string.Empty,
            Manufacturer = pack,
            Model = modeSplit > 0 ? rest[..modeSplit] : rest,
            Mode = modeSplit > 0 ? rest[(modeSplit + 1)..] : string.Empty,
            ChannelCount = 0,
            Capabilities = [],
            IsMissing = true
        };
    }
}
