namespace Model;

public class ShowService
{
    public List<Universe> Universes { get; set; } = [];
    public List<Fixture> Fixtures { get; set; } = [];
    public CueList CueList { get; set; } = new();
    public List<Macro> Macros { get; set; } = [];
    public List<Binding> Bindings { get; set; } = [];

    // Live console state (not persisted). When true, the channels named by BlackoutMasks are
    // transmitted as zeros while the underlying fader/cue state is left intact, so releasing it
    // instantly restores the look.
    public bool Blackout { get; set; }

    // Which channels blackout zeroes, per universe number: a 512-entry flag array, true where a
    // patched capability said it goes dark. Published copy-on-write so the Art-Net transmit thread
    // reads a complete map and never enumerates the fixture list while the UI is mutating it. A
    // universe with no entry has nothing patched that blackout touches, so its output is unchanged.
    private volatile IReadOnlyDictionary<byte, bool[]> _blackoutMasks = new Dictionary<byte, bool[]>();

    public IReadOnlyDictionary<byte, bool[]> BlackoutMasks => _blackoutMasks;

    /// <summary>
    /// Recomputes the blackout masks from the patch. Call from the UI thread after anything that
    /// changes which channels a fixture occupies: patching, unpatching, re-addressing, or moving a
    /// fixture to another universe.
    /// </summary>
    public void RefreshBlackoutMask()
    {
        var masks = new Dictionary<byte, bool[]>();

        foreach (var fixture in Fixtures)
            foreach (var capability in fixture.FixtureType.Capabilities)
            {
                if (capability.Blackout != BlackoutBehavior.Zero) continue;

                foreach (var offset in capability.Channels)
                {
                    var channel = fixture.Channel + offset;
                    if (channel is < 0 or > 511) continue; // a fixture addressed past the end of its universe

                    if (!masks.TryGetValue(fixture.UniverseNumber, out var mask))
                        masks[fixture.UniverseNumber] = mask = new bool[512];
                    mask[channel] = true;
                }
            }

        _blackoutMasks = masks;
    }

    public Universe? GetUniverse(byte number) =>
        Universes.FirstOrDefault(u => u.Number == number);
}
