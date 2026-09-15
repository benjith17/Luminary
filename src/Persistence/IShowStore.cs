using Fixtures;
using Model;

namespace Persistence;

// Persists a show to storage. The physical format (JSON today; could become MessagePack,
// SQLite, …) lives entirely behind this interface, so the model and view models never change.
public interface IShowStore
{
    void Save(ShowService show, string path);
    ShowService Load(string path, FixtureLibrary library);

    // The serialized form of a show, used both for saving and for change detection.
    string Serialize(ShowService show);
}
