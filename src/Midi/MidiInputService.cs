using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RtMidi.Core;
using RtMidi.Core.Devices;
using RtMidi.Core.Messages;

namespace Midi;

// MIDI input via RtMidi.Core, which bundles native RtMidi for Windows (WinMM), macOS (CoreMIDI) and
// Linux (ALSA/JACK). Opens every available input and raises a normalized event for Note-On /
// Control-Change. Fully defensive: no devices / a device that won't open leave the feature inactive.
//
// (Replaced managed-midi, whose WinMM backend cannot open input ports on .NET 10.)
public sealed class MidiInputService : IMidiInputService, IDisposable
{
    private readonly List<IMidiInputDevice> _open = [];
    private readonly List<string> _deviceNames = [];

    public IReadOnlyList<string> DeviceNames => _deviceNames;

    public event Action<MidiMessage>? MessageReceived;

    public Task StartAsync()
    {
        Stop();
        try
        {
            foreach (var info in MidiDeviceManager.Default.InputDevices)
            {
                try
                {
                    var device = info.CreateDevice();
                    var name = info.Name;

                    // RtMidi.Core decodes messages; Channel is 0-based (Channel1 == 0), Key/Control are 0–127.
                    device.ControlChange += (IMidiInputDevice _, in ControlChangeMessage e) =>
                        MessageReceived?.Invoke(new MidiMessage(name, (int)e.Channel + 1, MidiMessageKind.ControlChange, (int)e.Control, e.Value));

                    device.NoteOn += (IMidiInputDevice _, in NoteOnMessage e) =>
                    {
                        if (e.Velocity > 0) // velocity 0 == Note Off — ignored
                            MessageReceived?.Invoke(new MidiMessage(name, (int)e.Channel + 1, MidiMessageKind.NoteOn, (int)e.Key, e.Velocity));
                    };

                    device.Open();
                    _open.Add(device);
                    _deviceNames.Add(name);
                }
                catch
                {
                    // Skip a device that won't open.
                }
            }
        }
        catch
        {
            // No MIDI stack available — feature stays inactive.
        }

        return Task.CompletedTask;
    }

    public void Stop()
    {
        foreach (var device in _open)
        {
            try { device.Close(); device.Dispose(); } catch { /* ignore */ }
        }
        _open.Clear();
        _deviceNames.Clear();
    }

    public void Dispose() => Stop();
}
