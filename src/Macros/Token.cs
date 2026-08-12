namespace Macros;

// The macro language is line-oriented, so newlines are significant tokens (they terminate
// statements and delimit repeat-block bodies). Numbers are integers only: every '.' is a
// separator ('2.3' cue, 'Color.2' instance), and '..' is the range operator ('L1..8').
public enum TokenType
{
    Number,     // 1 or more digits, integer value in Number
    Ident,      // [A-Za-z][A-Za-z0-9]* — keywords, fixture refs (L5), capability names, unit suffixes
    At,         // @
    Plus,       // +
    Percent,    // %
    Dot,        // .
    DotDot,     // ..
    Underscore, // _  (leave-unchanged placeholder)
    Dollar,     // $  (input-value placeholder)
    Newline,
    Eof
}

public readonly record struct Token(TokenType Type, string Text, int Value, int Line, int Column)
{
    public override string ToString() => Type switch
    {
        TokenType.Number  => $"Number({Value})",
        TokenType.Ident   => $"Ident({Text})",
        TokenType.Newline => "Newline",
        TokenType.Eof     => "Eof",
        _                 => Text
    };
}
