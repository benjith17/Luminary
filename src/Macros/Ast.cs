namespace Macros;

// The parsed macro. Statements execute top to bottom; Diagnostics collects every parse problem so
// the editor can show them all at once (a program with errors is never run).
public sealed record MacroProgram(IReadOnlyList<Statement> Statements, IReadOnlyList<Diagnostic> Diagnostics)
{
    public bool HasErrors => Diagnostics.Any(d => d.Severity == Severity.Error);
}

// ---- Statements -------------------------------------------------------------------------------

public abstract record Statement
{
    // 1-based source line, so runtime errors can point back at the offending command.
    public int Line { get; init; }
}

// `go` — fire the selected cue and advance, exactly like the GO button.
public sealed record GoStatement : Statement;

// `goto 2` / `goto 2.3` / bare `2` / bare `2.3` — jump to a cue and fire it. Minor is null for a
// whole-number reference (interpreted as "the cue whose major is N", lowest minor).
public sealed record GotoStatement(int Major, int? Minor) : Statement;

// `wait 2s` / `wait 500ms` / `wait 2` — block the script for a duration.
public sealed record WaitStatement(TimeSpan Duration) : Statement;

// `repeat 4` … `end` — run Body Count times.
public sealed record RepeatStatement(int Count, IReadOnlyList<Statement> Body) : Statement;

// `loop` … `end` — run Body forever, until the macro is stopped. Rejected in binding contexts
// (a binding must finish), so it only appears in standalone macros.
public sealed record LoopStatement(IReadOnlyList<Statement> Body) : Statement;

// `blackout on` / `blackout off` / `blackout toggle` (bare `blackout` = toggle).
public sealed record BlackoutStatement(BlackoutMode Mode) : Statement;

public enum BlackoutMode { On, Off, Toggle }

// `next` / `prev` — move the cue selection without firing (like the Up/Down keybinds).
public sealed record SelectStatement(SelectDirection Direction) : Statement;

public enum SelectDirection { Next, Previous }

// A value-setting line: `L1..8 @ 80% fade 3s` or `L3 set Color 60% 100% 80%`.
public sealed record SetStatement(
    IReadOnlyList<SelectorTerm> Selector,
    ValueTarget Target,
    IReadOnlyList<ValueExpr> Values,
    TimeSpan? Fade) : Statement;

// ---- Selector ---------------------------------------------------------------------------------

// One term of a fixture selector. A single fixture is From == To; a range `L1..8` is inclusive.
public sealed record SelectorTerm(int From, int To)
{
    public bool IsRange => From != To;
}

// ---- Target -----------------------------------------------------------------------------------

public abstract record ValueTarget;

// `@` — resolve the target capability by how many values were given (arity inference).
public sealed record InferredTarget : ValueTarget;

// `set Color` / `set Color.2` — address a capability by macro name, optionally the Nth instance
// (1-based). Instance is null when unspecified (meaning the first/only one).
public sealed record NamedTarget(string Name, int? Instance) : ValueTarget;

// ---- Values -----------------------------------------------------------------------------------

public abstract record ValueExpr;

// `_` — leave this parameter untouched.
public sealed record KeepValue : ValueExpr;

// `80%` — scaled to the parameter's Max (resolution-independent).
public sealed record PercentValue(int Percent) : ValueExpr;

// `128` — written raw, clamped to the parameter's Max.
public sealed record RawValue(int Value) : ValueExpr;

// `$` — the driving input's value (a MIDI fader, a button). Resolves to that value scaled to the
// parameter's full range, so a control at its maximum yields the parameter's Max (255 / 100%).
public sealed record PlaceholderValue : ValueExpr;
