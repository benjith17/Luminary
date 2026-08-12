namespace Midi;

public enum MidiMessageKind
{
    NoteOn,        // a pad/key pressed (velocity > 0)
    ControlChange  // a fader/knob moved
}

// A normalized inbound MIDI message. Channel is 1–16; Number is the note or CC number (0–127);
// Value is velocity or CC value (0–127).
public readonly record struct MidiMessage(string Device, int Channel, MidiMessageKind Kind, int Number, int Value);
