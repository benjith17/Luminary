namespace Model;

// A per-show input binding: some trigger runs a macro-language action. Distinct from named Macros
// (which are trigger-less scripts) and from the fixed app shortcuts in settings.json. This is the
// only place bindings and the macro language meet — and the only place MIDI input is handled.
public sealed class Binding
{
    public string Name { get; set; } = "";
    public BindingTrigger Trigger { get; set; } = new();
    public string Action { get; set; } = ""; // macro-language source, e.g. "L1 @ $"
}

// What fires a binding. Kind selects the input type; only the fields relevant to that kind apply.
// MIDI fields are present so the show-file shape is stable; their handling lands with MIDI input.
public sealed class BindingTrigger
{
    public string Kind { get; set; } = "key"; // "key" | "midi"

    // key
    public string? Gesture { get; set; }        // e.g. "Ctrl+G"

    // midi
    public string? Device { get; set; }         // input device name
    public int? Channel { get; set; }           // 1–16
    public string? Message { get; set; }        // "note" | "cc"
    public int? Number { get; set; }            // note number or CC number
}
