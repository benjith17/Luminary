using System.Collections.Generic;
using System.Threading;
using Avalonia.Threading;
using Macros;

namespace ViewModel;

// Runs one input binding's macro. The source is parsed once and cached here, so firing — which can
// happen ~100×/second from a MIDI fader — never re-parses. Fire() runs the macro fire-and-forget on
// the UI thread, so it's safe to call from a background MIDI callback. A macro with parse errors
// never runs (its diagnostics are available for display).
public sealed class MacroBindingRunner
{
    private readonly IMacroHost _host;
    private readonly MacroProgram _program;

    public MacroBindingRunner(string source, IMacroHost host)
    {
        _host = host;
        _program = Parser.Parse(source);
    }

    public bool HasErrors => _program.HasErrors;
    public IReadOnlyList<Diagnostic> Diagnostics => _program.Diagnostics;

    // Fire the binding, passing the input value that `$` resolves to (MacroInput.None for a plain
    // button/key press that carries no value; the host then treats `$` as 0).
    public void Fire(MacroInput input)
    {
        if (_program.HasErrors) return;
        Dispatcher.UIThread.Post(() => _ = MacroInterpreter.RunAsync(_program, _host, input, CancellationToken.None));
    }
}
