namespace Macros;

public sealed record MacroRunResult(bool Completed, IReadOnlyList<Diagnostic> Diagnostics);

// Walks a parsed program and drives the host. Effects (go/goto/set) are dispatched to the host and
// are non-blocking — a cue's or a set's fade runs in the background while the script moves on.
// Only `wait` blocks, and it's the sequencing primitive. Runtime problems are collected, not thrown.
//
// RunAsync must be started on the UI thread: it awaits with the captured synchronization context, so
// every host call and every resumption after a wait lands back on the UI thread.
public static class MacroInterpreter
{
    public static async Task<MacroRunResult> RunAsync(MacroProgram program, IMacroHost host, CancellationToken ct)
    {
        var diagnostics = new List<Diagnostic>();
        void Report(Diagnostic d) => diagnostics.Add(d);

        var completed = true;
        try
        {
            await RunBlock(program.Statements, host, Report, ct);
        }
        catch (OperationCanceledException)
        {
            completed = false; // stopped by the user
        }
        return new MacroRunResult(completed, diagnostics);
    }

    private static async Task RunBlock(
        IReadOnlyList<Statement> statements, IMacroHost host, Action<Diagnostic> report, CancellationToken ct)
    {
        foreach (var statement in statements)
        {
            ct.ThrowIfCancellationRequested();
            switch (statement)
            {
                case GoStatement go:
                    host.Go(go, report);
                    break;

                case GotoStatement goto_:
                    host.GoTo(goto_, report);
                    break;

                case SetStatement set:
                    host.ApplySet(set, report);
                    break;

                case WaitStatement wait:
                    await Task.Delay(wait.Duration, ct);
                    break;

                case RepeatStatement repeat:
                    for (var i = 0; i < repeat.Count; i++)
                    {
                        ct.ThrowIfCancellationRequested();
                        // Yield each iteration so a wait-free loop can't freeze the UI or outrun Stop.
                        await Task.Yield();
                        await RunBlock(repeat.Body, host, report, ct);
                    }
                    break;
            }
        }
    }
}
