namespace Model;

// What a single property does while blackout is engaged.
//
// Blackout is an intensity kill, not a universe kill. Transmitting zeros across the whole frame
// takes the light out, but it also homes every moving head, winds zoom and focus to one end and
// drives control, mode and lamp channels to values that mean something other than "off" — a
// Robin Painte's Tint sits at 128 and its LED Frequency at 10, neither of which is safe at zero.
// So holding is the default, and a capability opts in to going dark.
public enum BlackoutBehavior
{
    Hold, // keep transmitting the live value
    Zero  // transmit zero on every channel of this capability
}
