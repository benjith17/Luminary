using System;
using System.Collections.Generic;
using System.Linq;
using Macros;

namespace ViewModel;

// Executes macro effects against the live show: resolves fixture numbers and capabilities, applies
// values to the manual layer (instantly or via a background fade), and drives the cue list for
// go/goto. Owned by the Macros window; one per run is fine. All calls arrive on the UI thread.
public sealed class MacroHost(
    FixturesListPanelViewModel fixtures,
    CueListPanelViewModel cues,
    Func<bool> getBlackout,
    Action<bool> setBlackout) : IMacroHost
{
    private readonly ManualFadeEngine _fades = new();

    // Stop every background fade this host started (called when a macro is stopped).
    public void CancelFades() => _fades.CancelAll();

    public void Go(GoStatement statement, Action<Diagnostic> report)
    {
        if (!cues.GoSelected())
            report(new Diagnostic(statement.Line, 0, "GO had no cue to fire."));
    }

    public void GoTo(GotoStatement statement, Action<Diagnostic> report)
    {
        if (!cues.GoTo(statement.Major, statement.Minor))
            report(new Diagnostic(statement.Line, 0, $"No cue {CueRef(statement)}."));
    }

    public void SetBlackout(BlackoutStatement statement, Action<Diagnostic> report) =>
        setBlackout(statement.Mode switch
        {
            BlackoutMode.On  => true,
            BlackoutMode.Off => false,
            _                => !getBlackout() // toggle
        });

    public void Select(SelectStatement statement, Action<Diagnostic> report)
    {
        if (statement.Direction == SelectDirection.Next) cues.SelectNext();
        else cues.SelectPrevious();
    }

    public void ApplySet(SetStatement statement, MacroInput input, Action<Diagnostic> report)
    {
        // A `$` with no driving input (e.g. a manual Run) resolves to 0 — flag it once so it's clear.
        if (input.Percent is null && statement.Values.Any(v => v is PlaceholderValue))
            report(new Diagnostic(statement.Line, 0,
                "'$' has no input value here; 0 is used (it's filled by a fader/button when this macro is bound to an input)."));

        foreach (var number in ExpandSelector(statement.Selector))
        {
            var fixture = fixtures.Fixtures.FirstOrDefault(f => f.Number == number);
            if (fixture is null)
            {
                report(new Diagnostic(statement.Line, 0, $"No fixture numbered {number}."));
                continue;
            }

            var capability = ResolveCapability(fixture, statement, report);
            if (capability is null) continue;

            ApplyValues(capability, statement, input);
        }
    }

    // Expands selector terms (singles and inclusive ranges) into distinct fixture numbers, in order.
    private static IEnumerable<int> ExpandSelector(IReadOnlyList<SelectorTerm> terms)
    {
        var seen = new HashSet<int>();
        foreach (var term in terms)
            for (var n = term.From; n <= term.To; n++)
                if (seen.Add(n))
                    yield return n;
    }

    // '@' picks the first capability whose parameter count equals the number of values (arity
    // inference). 'set Name[.n]' picks the named capability (the nth instance, 1-based).
    private static CapabilityViewModelBase? ResolveCapability(
        FixtureListItemViewModel fixture, SetStatement statement, Action<Diagnostic> report)
    {
        switch (statement.Target)
        {
            case InferredTarget:
            {
                var arity = statement.Values.Count;
                var match = fixture.Capabilities.FirstOrDefault(c => c.MacroParameters.Count == arity);
                if (match is null)
                    report(new Diagnostic(statement.Line, 0,
                        $"Fixture {fixture.Number} has no capability taking {arity} value{Plural(arity)} for '@'."));
                return match;
            }

            case NamedTarget named:
            {
                var matches = fixture.Capabilities
                    .Where(c => string.Equals(c.MacroName, named.Name, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (matches.Count == 0)
                {
                    report(new Diagnostic(statement.Line, 0,
                        $"Fixture {fixture.Number} has no '{named.Name}' capability."));
                    return null;
                }

                var index = (named.Instance ?? 1) - 1;
                if (index < 0 || index >= matches.Count)
                {
                    report(new Diagnostic(statement.Line, 0,
                        $"Fixture {fixture.Number} has {matches.Count} '{named.Name}' capabilit{(matches.Count == 1 ? "y" : "ies")}, not instance {named.Instance}."));
                    return null;
                }

                var capability = matches[index];
                if (statement.Values.Count > capability.MacroParameters.Count)
                {
                    report(new Diagnostic(statement.Line, 0,
                        $"'{named.Name}' takes at most {capability.MacroParameters.Count} value{Plural(capability.MacroParameters.Count)}, got {statement.Values.Count}."));
                    return null;
                }
                return capability;
            }
        }
        return null;
    }

    // Writes values positionally onto the capability's parameters. '_' leaves a parameter untouched;
    // '$' resolves to the driving input; a fade animates the manual layer, otherwise it snaps.
    private void ApplyValues(CapabilityViewModelBase capability, SetStatement statement, MacroInput input)
    {
        var count = Math.Min(statement.Values.Count, capability.MacroParameters.Count);
        for (var i = 0; i < count; i++)
        {
            if (statement.Values[i] is KeepValue) continue;

            var parameter = capability.MacroParameters[i];
            var target = statement.Values[i] switch
            {
                PercentValue p   => Scale(p.Percent, parameter.Max),
                RawValue r       => Math.Clamp(r.Value, 0, parameter.Max),
                PlaceholderValue => Scale(input.Percent ?? 0, parameter.Max),
                _                => parameter.Manual
            };

            if (statement.Fade is { } fade && fade > TimeSpan.Zero)
            {
                _fades.Start(parameter, target, fade);
            }
            else
            {
                _fades.Cancel(parameter);
                parameter.Manual = target;
            }
        }
    }

    // Scales a 0–100 percentage to a parameter's full range (so 100% → Max).
    private static int Scale(double percent, int max) =>
        (int)Math.Round(Math.Clamp(percent, 0, 100) / 100.0 * max);

    private static string CueRef(GotoStatement s) => s.Minor is { } m ? $"{s.Major}.{m}" : s.Major.ToString();
    private static string Plural(int n) => n == 1 ? "" : "s";
}
