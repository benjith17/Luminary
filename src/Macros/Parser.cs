namespace Macros;

// Recursive-descent parser for the macro language. It never throws on bad input: every problem is
// recorded as a Diagnostic and the parser recovers to the next line, so one typo doesn't hide the
// rest of the errors. The result always parses as far as it can for display; a program with any
// error diagnostic is refused execution by the interpreter.
public sealed class Parser
{
    private readonly List<Token> _tokens;
    private readonly List<Diagnostic> _diagnostics;
    private int _pos;

    private Parser(List<Token> tokens, List<Diagnostic> lexDiagnostics)
    {
        _tokens = tokens;
        _diagnostics = lexDiagnostics; // start from the lexer's diagnostics, then append our own
    }

    public static MacroProgram Parse(string source)
    {
        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens, lexer.Diagnostics);
        var statements = parser.ParseStatements(insideBlock: false);
        return new MacroProgram(statements, parser._diagnostics);
    }

    // ---- Statement list --------------------------------------------------------------------

    private List<Statement> ParseStatements(bool insideBlock)
    {
        var list = new List<Statement>();
        while (true)
        {
            SkipNewlines();
            if (Check(TokenType.Eof)) break;
            if (insideBlock && IsKeyword("end")) break; // leave 'end' for the repeat parser

            var stmt = ParseStatement();
            if (stmt is not null) list.Add(stmt);
        }
        return list;
    }

    private Statement? ParseStatement()
    {
        var tok = Peek();
        var line = tok.Line;

        // Bare cue number is shorthand for `goto`.
        if (tok.Type == TokenType.Number)
        {
            var cue = ParseCueRef();
            if (cue is null) { SkipToLineEnd(); return null; }
            return Terminate(new GotoStatement(cue.Value.Major, cue.Value.Minor) { Line = line });
        }

        if (tok.Type == TokenType.Ident)
        {
            switch (tok.Text.ToLowerInvariant())
            {
                case "go":
                    Advance();
                    return Terminate(new GoStatement { Line = line });
                case "goto":
                    Advance();
                    return ParseGoto(line);
                case "wait":
                    Advance();
                    return ParseWait(line);
                case "repeat":
                    Advance();
                    return ParseRepeat(line);
                case "end":
                    Advance();
                    Error(tok, "'end' has no matching 'repeat'.");
                    SkipToLineEnd();
                    return null;
                case "set":
                    Error(tok, "'set' must follow a fixture selector, e.g. 'L1 set Color …'.");
                    SkipToLineEnd();
                    return null;
                default:
                    // Anything else at line start must be a fixture selector (L5, L1..8, L1 + L3 …).
                    return ParseSetLine(line);
            }
        }

        Error(tok, $"Unexpected {Describe(tok)} at start of line.");
        SkipToLineEnd();
        return null;
    }

    // ---- go / goto -------------------------------------------------------------------------

    private Statement? ParseGoto(int line)
    {
        var cue = ParseCueRef();
        if (cue is null) { SkipToLineEnd(); return null; }
        return Terminate(new GotoStatement(cue.Value.Major, cue.Value.Minor) { Line = line });
    }

    private (int Major, int? Minor)? ParseCueRef()
    {
        if (!Check(TokenType.Number))
        {
            Error(Peek(), "Expected a cue number.");
            return null;
        }
        var major = Advance().Value;
        int? minor = null;
        if (Match(TokenType.Dot))
        {
            if (!Check(TokenType.Number))
            {
                Error(Peek(), "Expected a minor cue number after '.', e.g. '2.3'.");
                return null;
            }
            minor = Advance().Value;
        }
        return (major, minor);
    }

    // ---- wait ------------------------------------------------------------------------------

    private Statement? ParseWait(int line)
    {
        var dur = ParseDuration();
        if (dur is null) { SkipToLineEnd(); return null; }
        return Terminate(new WaitStatement(dur.Value) { Line = line });
    }

    private TimeSpan? ParseDuration()
    {
        if (!Check(TokenType.Number))
        {
            Error(Peek(), "Expected a duration, e.g. '2s' or '500ms'.");
            return null;
        }
        var n = Advance().Value;

        // A unit suffix lexes as its own ident ('2s' → Number(2) Ident(s)). Bare number = seconds.
        if (Check(TokenType.Ident))
        {
            var unit = Peek().Text.ToLowerInvariant();
            switch (unit)
            {
                case "s":  Advance(); return TimeSpan.FromSeconds(n);
                case "ms": Advance(); return TimeSpan.FromMilliseconds(n);
                default:
                    Error(Peek(), $"Unknown time unit '{Peek().Text}'. Use 's' or 'ms'.");
                    return null;
            }
        }
        return TimeSpan.FromSeconds(n);
    }

    // ---- repeat … end ----------------------------------------------------------------------

    private Statement? ParseRepeat(int line)
    {
        if (!Check(TokenType.Number))
        {
            Error(Peek(), "Expected a repeat count, e.g. 'repeat 4'.");
            SkipToLineEnd();
            return null;
        }
        var count = Advance().Value;

        if (!Check(TokenType.Newline) && !Check(TokenType.Eof))
        {
            Error(Peek(), "'repeat <count>' must be on its own line.");
            SkipToLineEnd();
        }

        var body = ParseStatements(insideBlock: true);

        if (IsKeyword("end"))
        {
            Advance();
            if (!Check(TokenType.Newline) && !Check(TokenType.Eof))
            {
                Error(Peek(), $"Unexpected {Describe(Peek())} after 'end'.");
                SkipToLineEnd();
            }
        }
        else
        {
            Error(Peek(), "Missing 'end' to close 'repeat'.");
        }

        return new RepeatStatement(count, body) { Line = line };
    }

    // ---- fixture value lines ---------------------------------------------------------------

    private Statement? ParseSetLine(int line)
    {
        var selector = ParseSelector();
        if (selector is null) { SkipToLineEnd(); return null; }

        ValueTarget target;
        if (Match(TokenType.At))
        {
            target = new InferredTarget();
        }
        else if (IsKeyword("set"))
        {
            Advance();
            var cap = ParseCapRef();
            if (cap is null) { SkipToLineEnd(); return null; }
            target = cap;
        }
        else
        {
            Error(Peek(), $"Expected '@' or 'set' after the fixture selector, but found {Describe(Peek())}.");
            SkipToLineEnd();
            return null;
        }

        var values = ParseValues();
        if (values.Count == 0)
        {
            Error(Peek(), "Expected at least one value.");
            SkipToLineEnd();
            return null;
        }

        TimeSpan? fade = null;
        if (IsKeyword("fade"))
        {
            Advance();
            var dur = ParseDuration();
            if (dur is null) { SkipToLineEnd(); return null; }
            fade = dur;
        }

        return Terminate(new SetStatement(selector, target, values, fade) { Line = line });
    }

    private IReadOnlyList<SelectorTerm>? ParseSelector()
    {
        var terms = new List<SelectorTerm>();
        var first = ParseSelectorTerm();
        if (first is null) return null;
        terms.Add(first);

        while (Match(TokenType.Plus))
        {
            var next = ParseSelectorTerm();
            if (next is null) return null;
            terms.Add(next);
        }
        return terms;
    }

    private SelectorTerm? ParseSelectorTerm()
    {
        if (!Check(TokenType.Ident) || !TryFixtureNumber(Peek().Text, out var from))
        {
            Error(Peek(), $"Expected a fixture reference like 'L5', but found {Describe(Peek())}.");
            return null;
        }
        Advance();

        var to = from;
        if (Match(TokenType.DotDot))
        {
            // Range end: bare number ('L1..8'); an 'L'-prefixed end ('L1..L8') is also tolerated.
            if (Check(TokenType.Number))
            {
                to = Advance().Value;
            }
            else if (Check(TokenType.Ident) && TryFixtureNumber(Peek().Text, out var hi))
            {
                Advance();
                to = hi;
            }
            else
            {
                Error(Peek(), "Expected a fixture number after '..', e.g. 'L1..8'.");
                return null;
            }
        }

        if (from < 1 || to < 1)
        {
            Error(Peek(), "Fixture numbers start at 1.");
            return null;
        }
        if (to < from) (from, to) = (to, from); // tolerate a reversed range
        return new SelectorTerm(from, to);
    }

    private NamedTarget? ParseCapRef()
    {
        if (!Check(TokenType.Ident))
        {
            Error(Peek(), "Expected a capability name after 'set', e.g. 'Color'.");
            return null;
        }
        var name = Advance().Text;

        int? instance = null;
        if (Match(TokenType.Dot))
        {
            if (!Check(TokenType.Number))
            {
                Error(Peek(), "Expected an instance number after '.', e.g. 'Color.2'.");
                return null;
            }
            instance = Advance().Value;
            if (instance < 1)
            {
                Error(Peek(), "Capability instances start at 1.");
                return null;
            }
        }
        return new NamedTarget(name, instance);
    }

    private List<ValueExpr> ParseValues()
    {
        var values = new List<ValueExpr>();
        while (true)
        {
            if (Check(TokenType.Underscore))
            {
                Advance();
                values.Add(new KeepValue());
            }
            else if (Check(TokenType.Number))
            {
                var n = Advance().Value;
                values.Add(Match(TokenType.Percent) ? new PercentValue(n) : new RawValue(n));
            }
            else
            {
                break; // 'fade', newline, eof, or anything else ends the value list
            }
        }
        return values;
    }

    // ---- helpers ---------------------------------------------------------------------------

    // After a complete statement, the rest of the line must be empty.
    private Statement Terminate(Statement stmt)
    {
        if (!Check(TokenType.Newline) && !Check(TokenType.Eof))
        {
            Error(Peek(), $"Unexpected {Describe(Peek())} after end of command.");
            SkipToLineEnd();
        }
        return stmt;
    }

    private static bool TryFixtureNumber(string text, out int number)
    {
        number = 0;
        if (text.Length < 2 || text[0] is not ('L' or 'l')) return false;
        return int.TryParse(text.AsSpan(1), out number);
    }

    private Token Peek() => _tokens[_pos];
    private bool Check(TokenType type) => _tokens[_pos].Type == type;
    private bool IsKeyword(string word) => Check(TokenType.Ident) && Peek().Text.Equals(word, StringComparison.OrdinalIgnoreCase);

    private Token Advance()
    {
        var tok = _tokens[_pos];
        if (tok.Type != TokenType.Eof) _pos++;
        return tok;
    }

    private bool Match(TokenType type)
    {
        if (!Check(type)) return false;
        Advance();
        return true;
    }

    private void SkipNewlines()
    {
        while (Check(TokenType.Newline)) Advance();
    }

    private void SkipToLineEnd()
    {
        while (!Check(TokenType.Newline) && !Check(TokenType.Eof)) Advance();
    }

    private void Error(Token at, string message) =>
        _diagnostics.Add(new Diagnostic(at.Line, at.Column, message));

    private static string Describe(Token t) => t.Type switch
    {
        TokenType.Newline => "end of line",
        TokenType.Eof     => "end of macro",
        TokenType.Number  => $"'{t.Value}'",
        TokenType.At      => "'@'",
        TokenType.Plus    => "'+'",
        TokenType.Percent => "'%'",
        TokenType.Dot     => "'.'",
        TokenType.DotDot  => "'..'",
        TokenType.Underscore => "'_'",
        _                 => $"'{t.Text}'"
    };
}
