namespace Macros;

public sealed record MacroRunResult(bool Completed, IReadOnlyList<Diagnostic> Diagnostics);

// The scalar a driving input feeds into a run: the value the `$` placeholder resolves to, as a
// percentage (0–100). None when a run has no input (e.g. the Macros window's Run button).
public readonly record struct MacroInput(double? Percent)
{
    public static readonly MacroInput None = new((double?)null);

    public static MacroInput FromPercent(double percent) => new(percent);
}

// Walks a parsed program and drives the host. Effects (go/goto/set) are dispatched to the host and
// are non-blocking — a cue's or a set's fade runs in the background while the script moves on.
// Only `wait` blocks, and it's the sequencing primitive. Runtime problems are collected, not thrown.
//
// RunAsync must be started on the UI thread: it awaits with the captured synchronization context, so
// every host call and every resumption after a wait lands back on the UI thread.
public static class MacroInterpreter
{
    public static async Task<MacroRunResult> RunAsync(
        MacroProgram program, IMacroHost host, MacroInput input, CancellationToken ct)
    {
        var diagnostics = new List<Diagnostic>();
        void Report(Diagnostic d) => diagnostics.Add(d);

        var completed = true;
        try
        {
            await RunBlock(program.Statements, host, input, Report, ct);
        }
        catch (OperationCanceledException)
        {
            completed = false; // stopped by the user
        }
        return new MacroRunResult(completed, diagnostics);
    }

    private static async Task RunBlock(
        IReadOnlyList<Statement> statements, IMacroHost host, MacroInput input, Action<Diagnostic> report, CancellationToken ct)
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
                    host.ApplySet(set, input, report);
                    break;

                case BlackoutStatement blackout:
                    host.SetBlackout(blackout, report);
                    break;

                case SelectStatement select:
                    host.Select(select, report);
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
                        await RunBlock(repeat.Body, host, input, report, ct);
                    }
                    break;

                case LoopStatement loop:
                    while (true)
                    {
                        ct.ThrowIfCancellationRequested();
                        // Yield each pass so a wait-free loop stays cancellable and never freezes the UI.
                        await Task.Yield();
                        await RunBlock(loop.Body, host, input, report, ct);
                    }
            }
        }
    }
}
