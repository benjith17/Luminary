namespace Model;

// How a universe's channel data leaves the app. New transports (sACN, USB-DMX, …) add a
// subclass here and a matching branch in the output service.
public abstract class UniverseOutput;

// Art-Net 3 style: broadcast (or fixed-IP unicast) ArtDmx, no discovery. Simple and compatible
// with the vast majority of gear.
public sealed class ArtNetOutput : UniverseOutput
{
    // Limited broadcast by default: received by anything on the local network regardless of its
    // IP scheme. Can be set to a specific node IP or a directed subnet broadcast (e.g. 2.255.255.255).
    public string Ip { get; set; } = "255.255.255.255";
    public int Port { get; set; } = 6454;
    public int ArtNetUniverse { get; set; }
}

// Art-Net 4 style: discover subscribers via ArtPoll / ArtPollReply and unicast ArtDmx only to them.
// ManualTargets are always-unicast IPs, a fallback for nodes that don't answer ArtPoll.
public sealed class ArtNet4Output : UniverseOutput
{
    public int ArtNetUniverse { get; set; }               // 15-bit Port-Address, 0–32767
    public int Port { get; set; } = 6454;
    public List<string> ManualTargets { get; set; } = [];
}
