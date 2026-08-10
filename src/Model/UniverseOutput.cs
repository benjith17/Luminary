namespace Model;

// How a universe's channel data leaves the app. New transports (sACN, USB-DMX, …) add a
// subclass here and a matching branch in the output service.
public abstract class UniverseOutput;

public sealed class ArtNetOutput : UniverseOutput
{
    public string Ip { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 6454;
    public int ArtNetUniverse { get; set; }
}
