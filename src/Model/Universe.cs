namespace Model;

public class Universe(byte number, string targetIp = "127.0.0.1")
{
    public byte Number { get; init; } = number;
    public string TargetIp { get; set; } = targetIp;
    public byte[] Channels { get; private set; } = new byte[512];

    public FixtureGroup[] Group { get; set; } = [];

    public void Set(int channel, byte value)
    {
        if (channel < 0 || channel > 511)
            throw new ArgumentOutOfRangeException(nameof(channel), "Channel must be between 0 and 511.");

        Channels[channel] = value;
    }

    public void SetRange(int startChannel, byte[] values)
    {
        if (startChannel < 0 || startChannel > 511)
            throw new ArgumentOutOfRangeException(nameof(startChannel), "Start channel must be between 0 and 511.");
        if (startChannel + values.Length > 512)
            throw new ArgumentException("Values exceed universe channel limit.");

        Array.Copy(values, 0, Channels, startChannel, values.Length);
    }

    public void Clear() => Array.Clear(Channels, 0, Channels.Length);
}