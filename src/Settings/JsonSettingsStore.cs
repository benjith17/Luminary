using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Settings;

// JSON settings persisted to a per-user app-data file. The dictionary keys (action names) are written
// verbatim — PropertyNamingPolicy only touches property names, not dictionary keys — so the file
// stays human-readable and hand-editable.
public sealed class JsonSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        // The default encoder escapes '+' (and '<', '>', '&') as \u00XX. This is a hand-editable
        // local file, so use the relaxed encoder to keep gestures like "Alt+B" readable.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly string _path;

    // Defaults to <AppData>/Luminary/settings.json; overridable for tests.
    public JsonSettingsStore(string? path = null)
    {
        _path = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Luminary", "settings.json");
    }

    public AppSettings Load()
    {
        AppSettings settings;
        try
        {
            settings = File.Exists(_path)
                ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path), Options) ?? new AppSettings()
                : new AppSettings();
        }
        catch
        {
            settings = new AppSettings(); // unreadable / corrupt → fall back to defaults
        }

        // Back-fill any action missing from the file (older files, or newly added actions).
        foreach (var (bind, gesture) in KeybindDefaults.Gestures)
            settings.Keybindings.TryAdd(bind.ToString(), gesture);

        return settings;
    }

    public void Save(AppSettings settings)
    {
        try
        {
            var dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(_path, JsonSerializer.Serialize(settings, Options));
        }
        catch
        {
            // Non-fatal: settings simply won't persist this run.
        }
    }
}
