namespace ViewModel;

// How a single property behaves during a crossfade.
public enum FadeBehavior
{
    Fade, // interpolate smoothly from the current value to the cue value
    Snap  // jump straight to the cue value the moment the cue is fired
}

public abstract class CapabilityViewModelBase(string name) : ViewModelBase
{
    public string Name { get; } = name;

    public abstract byte[] Capture();
    public abstract void Restore(byte[] values);

    // Each capability declares its properties here, in the same order and byte-width they
    // appear in Capture()/Restore(). This is where fade-vs-snap is attributed per property.
    protected abstract FadeParam[] BuildFadeParams();

    private FadeParam[]? _fadeParams;
    private FadeParam[] FadeParams => _fadeParams ??= BuildFadeParams();

    // Interpolate from a captured start state toward a captured target at progress t (0..1).
    // Snap properties jump to the target; fade properties interpolate. 16-bit properties are
    // reconstructed from their two bytes so they interpolate as a single value.
    public void ApplyLerp(byte[] from, byte[] to, double t)
    {
        var offset = 0;
        foreach (var param in FadeParams)
        {
            var start = Read(from, offset, param.Width);
            var target = Read(to, offset, param.Width);
            var value = param.Behavior == FadeBehavior.Snap
                ? target
                : (int)Math.Round(start + (target - start) * t);
            param.Set(value);
            offset += param.Width;
        }
    }

    private static int Read(byte[] data, int offset, int width) =>
        width == 2 ? (data[offset] << 8) | data[offset + 1] : data[offset];

    // One controllable property: how many bytes it occupies in the snapshot, whether it
    // fades or snaps, and how to write the resolved value back to the capability.
    protected readonly record struct FadeParam(int Width, FadeBehavior Behavior, Action<int> Set);
}
