using System.Net;

namespace ArtNet;

// Tracks Art-Net nodes discovered via ArtPollReply and which universes each subscribes to.
//
// Threading: the receive loop mutates the table (Ingest/Expire) under a lock; the transmit loop
// reads subscribers via a lock-free immutable snapshot that's republished on every change. This
// mirrors the copy-on-write pattern already used for ShowService.Universes.
public sealed class NodeTable
{
    // Nodes not heard from within this window are treated as disconnected. The spec permits a 3s
    // assumption; we allow a little slack to tolerate an occasional lost reply.
    public const double TimeoutSeconds = 6.0;

    private readonly Lock _gate = new();
    private readonly Dictionary<(string Ip, int Bind), Entry> _entries = [];

    // Published snapshot: universe (Port-Address) → subscriber IPs. Read without locking.
    private volatile IReadOnlyDictionary<int, IPAddress[]> _snapshot =
        new Dictionary<int, IPAddress[]>();

    private sealed class Entry
    {
        public required IPAddress Ip { get; init; }
        public required HashSet<int> Universes { get; set; }
        public double LastSeen { get; set; }
    }

    /// <summary>
    /// Records (or refreshes) a node from a parsed ArtPollReply.
    /// </summary>
    public void Ingest(ParsedReply reply, double now)
    {
        lock (_gate)
        {
            _entries[(reply.Ip.ToString(), reply.BindIndex)] = new Entry
            {
                Ip = reply.Ip,
                Universes = reply.SubscribedUniverses,
                LastSeen = now
            };
            Rebuild();
        }
    }

    /// <summary>
    /// Drops nodes not seen within the timeout. Call after each poll cycle.
    /// </summary>
    public void Expire(double now)
    {
        lock (_gate)
        {
            var stale = _entries
                .Where(kv => now - kv.Value.LastSeen > TimeoutSeconds)
                .Select(kv => kv.Key)
                .ToList();

            if (stale.Count == 0) return;
            foreach (var key in stale) _entries.Remove(key);
            Rebuild();
        }
    }

    /// <summary>
    /// The IPs subscribed to a universe (Port-Address). Empty if none. Lock-free.
    /// </summary>
    public IReadOnlyList<IPAddress> SubscribersFor(int universe) =>
        _snapshot.TryGetValue(universe, out var ips) ? ips : [];

    // Rebuilds the immutable universe→IPs snapshot. Caller holds _gate.
    private void Rebuild()
    {
        var map = new Dictionary<int, HashSet<IPAddress>>();
        foreach (var entry in _entries.Values)
        {
            foreach (var universe in entry.Universes)
            {
                if (!map.TryGetValue(universe, out var set))
                    map[universe] = set = [];
                set.Add(entry.Ip);
            }
        }

        _snapshot = map.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray());
    }
}
