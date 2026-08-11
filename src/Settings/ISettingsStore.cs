namespace Settings;

// Abstracts where and how application settings are persisted, mirroring IShowStore. Load never fails:
// a missing or corrupt file yields defaults, and any action absent from the file is back-filled.
public interface ISettingsStore
{
    AppSettings Load();
    void Save(AppSettings settings);
}
