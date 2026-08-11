namespace Macros;

// A problem found while compiling or running a macro, addressed to a source line so the editor
// can point at it. Line/Column are 1-based; Column is best-effort (0 when not meaningful).
public enum Severity { Error, Warning }

public sealed record Diagnostic(int Line, int Column, string Message, Severity Severity = Severity.Error)
{
    public override string ToString() =>
        $"Line {Line}: {(Severity == Severity.Warning ? "warning: " : "")}{Message}";
}
