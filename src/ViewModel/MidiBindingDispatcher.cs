using System;
using System.Collections.Generic;
using Macros;
using Midi;
using Model;

namespace ViewModel;

// Routes inbound MIDI messages to the per-show bindings that match, firing each binding's macro with
// the message value as the `$` input (0–127 → 0–100%). Built per show from its MIDI-kind bindings;
// each binding's macro is parsed once and cached inside its MacroBindingRunner.
public sealed class MidiBindingDispatcher
{
    private readonly List<(BindingTrigger Trigger, MacroBindingRunner Runner)> _bindings = [];

    public MidiBindingDispatcher(IEnumerable<Binding> bindings, IMacroHost host)
    {
        foreach (var binding in bindings)
        {
            if (!string.Equals(binding.Trigger.Kind, "midi", StringComparison.OrdinalIgnoreCase)) continue;
            _bindings.Add((binding.Trigger, new MacroBindingRunner(binding.Action, host)));
        }
    }

    // Called for each inbound MIDI message (on the MIDI thread). Matching runners marshal to the UI
    // thread themselves, so this is safe to call from any thread.
    public void Dispatch(MidiMessage message)
    {
        var percent = message.Value / 127.0 * 100.0;
        foreach (var (trigger, runner) in _bindings)
        {
            if (Matches(trigger, message))
                runner.Fire(MacroInput.FromPercent(percent));
        }
    }

    private static bool Matches(BindingTrigger trigger, MidiMessage message)
    {
        if (trigger.IgnoreZero && message.Value == 0) return false; // e.g. a CC button's release
        if (trigger.Channel is { } channel && channel != message.Channel) return false;
        if (trigger.Number is { } number && number != message.Number) return false;

        if (trigger.Message == "cc" && message.Kind != MidiMessageKind.ControlChange) return false;
        if (trigger.Message == "note" && message.Kind != MidiMessageKind.NoteOn) return false;

        if (!string.IsNullOrEmpty(trigger.Device) &&
            !string.Equals(trigger.Device, message.Device, StringComparison.OrdinalIgnoreCase)) return false;

        return true;
    }
}
