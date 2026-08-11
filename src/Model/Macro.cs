namespace Model;

// A named, user-authored macro: the DSL source text. Parsed and run on demand (never precompiled),
// so the show file stays a plain, forward-compatible record of what the user typed.
public sealed class Macro
{
    public string Name { get; set; } = "Macro";
    public string Source { get; set; } = string.Empty;
}
