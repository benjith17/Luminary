namespace Model;

// How a single property behaves during a crossfade.
//
// Snap is for values where the intermediate positions are meaningless or ugly — an indexed colour
// or gobo wheel sweeping through every slot, a shutter crossing its strobe ranges, a pan/tilt
// sweeping the rig across the stage on cue recall.
//
// Note this is honoured for crossfades only. Between keyframes the authored curve wins; see the
// note on CapabilityViewModelBase.Interpolate.
public enum FadeBehavior
{
    Fade, // interpolate smoothly from the current value to the cue value
    Snap  // jump straight to the cue value the moment the cue is fired
}
