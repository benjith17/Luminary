namespace Macros;

// The bridge between the pure interpreter and the live show. The interpreter owns control flow
// (sequencing, repeat, waits, cancellation); the host performs the effects against the actual
// fixtures and cue list. Every method may report runtime problems (unknown fixture, no matching
// capability, missing cue) via the supplied sink — these never abort the run, they just surface.
//
// All host methods are called on the UI thread, in order, one at a time.
public interface IMacroHost
{
    // Fire the selected cue and advance, exactly like the GO button.
    void Go(GoStatement statement, Action<Diagnostic> report);

    // Jump to a cue by number and fire it.
    void GoTo(GotoStatement statement, Action<Diagnostic> report);

    // Apply a value command to every fixture in the selector. `input` supplies the value for any
    // `$` placeholder in the command.
    void ApplySet(SetStatement statement, MacroInput input, Action<Diagnostic> report);

    // Set / clear / toggle blackout.
    void SetBlackout(BlackoutStatement statement, Action<Diagnostic> report);

    // Move the cue selection (next / previous) without firing.
    void Select(SelectStatement statement, Action<Diagnostic> report);
}
