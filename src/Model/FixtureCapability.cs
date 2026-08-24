namespace Model;

public abstract class FixtureCapability(string name, int offset, byte defaultValue = 0)
{
    public string Name { get; set; } = name;
    public int Offset { get; set; } = offset;
    public byte Default { get; set; } = defaultValue;

    // Macro/DSL identity. MacroName is the keyword used by `set <name>`; together with
    // MacroParameters.Count it drives `@` arity inference. Defined per capability so that
    // plug-in capabilities are addressable from macros without any hard-coded knowledge here.
    public abstract string MacroName { get; }
    public abstract IReadOnlyList<string> MacroParameters { get; }
}