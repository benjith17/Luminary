namespace Settings;

// Owns the live application settings for the session. Loads them at startup (back-filling defaults),
// then writes them straight back so the on-disk file is seeded on first run and upgraded with any
// newly added defaults — leaving a complete, editable file for the user.
public sealed class SettingsService
{
    private readonly ISettingsStore _store;

    public AppSettings Settings { get; }

    public SettingsService(ISettingsStore? store = null)
    {
        _store = store ?? new JsonSettingsStore();
        Settings = _store.Load();
        _store.Save(Settings);
    }

    public void Save() => _store.Save(Settings);

    // The gesture string bound to an action, or null if it's unbound / blank.
    public string? GestureFor(Keybind bind) =>
        Settings.Keybindings.TryGetValue(bind.ToString(), out var gesture) && !string.IsNullOrWhiteSpace(gesture)
            ? gesture
            : null;
}
