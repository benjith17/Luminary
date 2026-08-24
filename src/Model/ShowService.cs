namespace Model;

public class ShowService
{
    public List<Universe> Universes { get; set; } = [];
    public List<Fixture> Fixtures { get; set; } = [];
    public CueList CueList { get; set; } = new();
    public List<Macro> Macros { get; set; } = [];
    public List<Binding> Bindings { get; set; } = [];

    // Live console state (not persisted). When true, all output is transmitted as zeros while the
    // underlying fader/cue state is left intact, so releasing it instantly restores the look.
    public bool Blackout { get; set; }

    public Universe? GetUniverse(byte number) =>
        Universes.FirstOrDefault(u => u.Number == number);
}
