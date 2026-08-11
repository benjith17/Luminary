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

    // Apply a value command to every fixture in the selector.
    void ApplySet(SetStatement statement, Action<Diagnostic> report);
}
