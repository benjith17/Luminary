using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Midi;

// Access to MIDI input, decoupled from the concrete library so it can be swapped later.
public interface IMidiInputService
{
    // Names of the input devices currently opened.
    IReadOnlyList<string> DeviceNames { get; }

    // Raised (on a background MIDI thread) for each Note-On / Control-Change message.
    event Action<MidiMessage>? MessageReceived;

    // Open all available MIDI inputs. Safe to call when there are none or no MIDI stack is present.
    Task StartAsync();

    // Close all opened inputs.
    void Stop();
}
