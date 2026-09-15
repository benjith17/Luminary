namespace Model;

public abstract class FixtureCapability(string name, int offset, byte defaultValue = 0)
{
    public string Name { get; set; } = name;
    public int Offset { get; set; } = offset;
    public byte Default { get; set; } = defaultValue;

    // Which attribute family this capability belongs to, from the wrapper element that contained
    // it in the fixture file. Set by the loader rather than the constructor, so a capability type
    // valid in several families (level, and plug-in capabilities) needs no special handling.
    public CapabilityFamily Family { get; set; }

    // Marks this as its family's target for a macro's '@' shorthand, where the family holds more
    // than one candidate. The first primary wins; absent any, the first in the family is used.
    public bool Primary { get; set; }

    // Every DMX channel this capability occupies, as 0-based offsets. Used to check a personality
    // fits its declared footprint and that no two capabilities fight over a channel. Multi-channel
    // capabilities must override; the default covers the single-channel case.
    public virtual IEnumerable<int> Channels => [Offset];

    // Macro/DSL identity. MacroName is the keyword used by `set <name>`; together with
    // MacroParameters.Count it drives `@` arity inference. Defined per capability so that
    // plug-in capabilities are addressable from macros without any hard-coded knowledge here.
    public abstract string MacroName { get; }
    public abstract IReadOnlyList<string> MacroParameters { get; }
}