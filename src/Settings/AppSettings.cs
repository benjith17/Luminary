namespace Settings;

// An action that can be bound to a key. Persisted by name (see AppSettings.Keybindings), so renaming
// a value is a breaking change to existing settings files — add new actions rather than rename.
public enum Keybind
{
    Go,
    SelectPreviousCue,
    SelectNextCue,
    Blackout,
    Patch,
    Fullscreen,
    CloseWindow
}

// Machine-local, per-user application settings — independent of any show. Loaded once at startup and
// shared across every show (it lives outside the LoadShow swap). Keybindings map a Keybind, by name,
// to an Avalonia KeyGesture string (e.g. "Space", "Ctrl+Shift+B").
public sealed class AppSettings
{
    public int Version { get; set; } = 1;
    public Dictionary<string, string> Keybindings { get; set; } = new();
}

// Default gestures — applied on first run and back-filled whenever an action is missing from the
// file, so a newly added action is never left unbound for existing users. "Mod" is the platform's
// primary accelerator: Cmd on macOS, Ctrl elsewhere (see KeybindingController).
public static class KeybindDefaults
{
    public static IReadOnlyDictionary<Keybind, string> Gestures { get; } = new Dictionary<Keybind, string>
    {
        [Keybind.Go]                = "Space",
        [Keybind.SelectPreviousCue] = "Up",
        [Keybind.SelectNextCue]     = "Down",
        [Keybind.Blackout]          = "Alt+B",
        [Keybind.Patch]             = "Alt+P",
        [Keybind.Fullscreen]        = "F11",
        [Keybind.CloseWindow]       = "Mod+W",
    };
}
