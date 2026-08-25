using System;
using System.Collections.Generic;
using System.Linq;
using Model;

namespace ViewModel;

// How a single property behaves during a crossfade.
public enum FadeBehavior
{
    Fade, // interpolate smoothly from the current value to the cue value
    Snap  // jump straight to the cue value the moment the cue is fired
}

public abstract class CapabilityViewModelBase(FixtureCapability capability) : ViewModelBase
{
    public string Name { get; } = capability.Name;

    // Macro/DSL identity, carried from the model capability so the interpreter can address this
    // capability by name (`set Color …`) and by parameter count (`@` arity inference).
    public string MacroName { get; } = capability.MacroName;
    public IReadOnlyList<string> MacroParameterNames { get; } = capability.MacroParameters;

    // Public view of the parameters for the macro interpreter: read each parameter's Max (for
    // percent scaling) and drive its Manual layer. Ordered to match MacroParameterNames.
    public IReadOnlyList<CapabilityParameter> MacroParameters => Parameters;

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
            param.SetPlayback(Blend(param, from, to, offset, t, honourSnap: true));
            param.PlaybackActive = true;
            offset += param.Width;
        }
    }

    // Drives the playback layer straight to a set of values, with no interpolation. Used by the
    // keyframe engine, which does its own blending and applies the result in one write.
    public void ApplyValues(byte[] values)
    {
        var offset = 0;
        foreach (var param in Parameters)
        {
            param.SetPlayback(Read(values, offset, param.Width));
            param.PlaybackActive = true;
            offset += param.Width;
        }
    }

    // Crossfade blend, returned rather than applied: snap parameters jump, fade parameters
    // interpolate. Same rule as ApplyLerp — use this where a fade's result is needed as a value.
    public byte[] Lerp(byte[] from, byte[] to, double t) => Blend(from, to, t, honourSnap: true);

    // Authored-curve blend: interpolates every parameter, including ones marked Snap.
    //
    // FadeBehavior describes what should happen during a *crossfade*, where the console picks the
    // path — pan/tilt is marked Snap so positions don't sweep on cue recall. Between two keyframes
    // the author has said explicitly where the value should be and when, so the curve wins;
    // honouring Snap here would make every moving-head track teleport between its keys. A
    // parameter that genuinely must not sweep (a gobo or colour wheel) gets a Hold keyframe.
    public byte[] Interpolate(byte[] from, byte[] to, double t) => Blend(from, to, t, honourSnap: false);

    private byte[] Blend(byte[] from, byte[] to, double t, bool honourSnap)
    {
        var bytes = new byte[Parameters.Sum(p => p.Width)];
        var offset = 0;

        foreach (var param in Parameters)
        {
            var value = Blend(param, from, to, offset, t, honourSnap);
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

    private static int Blend(CapabilityParameter param, byte[] from, byte[] to, int offset, double t, bool honourSnap)
    {
        var start = Read(from, offset, param.Width);
        var target = Read(to, offset, param.Width);

        return honourSnap && param.Fade == FadeBehavior.Snap
            ? target
            : (int)Math.Round(start + (target - start) * t);
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
