namespace Model;

// The console-standard attribute families (IFCB). Every capability belongs to exactly one, which
// drives how the fixture is presented and how a macro's '@' shorthand routes its values.
//
// Declared in display order: this is the order operators expect to see across consoles.
public enum CapabilityFamily
{
    Intensity,
    Color,
    Focus,
    Beam
}
