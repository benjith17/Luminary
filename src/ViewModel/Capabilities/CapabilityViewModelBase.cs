using System;
using System.Collections.Generic;
using System.Linq;

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

    // A capability's controllable values, ordered to match the DMX snapshot layout: each 8-bit
    // parameter is one byte, each 16-bit parameter is two (MSB then LSB). This is where each
    // property's merge (HTP/LTP) and fade (fade/snap) behaviour is attributed.
    protected abstract IReadOnlyList<CapabilityParameter> Parameters { get; }

    // Captures the merged output — used both for recording ("record what you see") and as the
    // start point of a crossfade ("fade from what's currently on stage").
    public byte[] Capture() => Pack(p => p.Output);

    // A cue drives the playback layer: snap parameters jump to target, fade parameters interpolate.
    // 16-bit parameters are reconstructed from their two bytes so they interpolate as one value.
    public void ApplyLerp(byte[] from, byte[] to, double t)
    {
        var offset = 0;
        foreach (var param in Parameters)
        {
            var start = Read(from, offset, param.Width);
            var target = Read(to, offset, param.Width);
            param.Playback = param.Fade == FadeBehavior.Snap
                ? target
                : (int)Math.Round(start + (target - start) * t);
            param.PlaybackActive = true;
            offset += param.Width;
        }
    }

    // Re-emit every parameter's current output to DMX (used after a re-patch clears the universe).
    public void PushOutput()
    {
        foreach (var param in Parameters) param.PushOutput();
    }

    private byte[] Pack(Func<CapabilityParameter, int> select)
    {
        var bytes = new byte[Parameters.Sum(p => p.Width)];
        var offset = 0;
        foreach (var param in Parameters)
        {
            var value = select(param);
            if (param.Width == 2)
            {
                bytes[offset++] = (byte)(value >> 8);
                bytes[offset++] = (byte)(value & 0xFF);
            }
            else
            {
                bytes[offset++] = (byte)value;
            }
        }
        return bytes;
    }

    private static int Read(byte[] data, int offset, int width) =>
        width == 2 ? (data[offset] << 8) | data[offset + 1] : data[offset];
}
