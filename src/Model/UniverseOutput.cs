namespace Model;

// How a universe's channel data leaves the app. New transports (sACN, USB-DMX, …) add a
// subclass here and a matching branch in the output service.
public abstract class UniverseOutput;

public sealed class ArtNetOutput : UniverseOutput
{
    // Limited broadcast by default: received by anything on the local network regardless of its
    // IP scheme. Can be set to a specific node IP or a directed subnet broadcast (e.g. 2.255.255.255).
    public string Ip { get; set; } = "255.255.255.255";
    public int Port { get; set; } = 6454;
    public int ArtNetUniverse { get; set; }
}
