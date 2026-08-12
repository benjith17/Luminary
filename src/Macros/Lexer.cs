using System.Text;

namespace Macros;

// Turns macro source into a flat token stream. Whitespace (except newlines) and '#' comments are
// skipped; anything it can't classify becomes a diagnostic and is dropped, so the parser always
// receives clean tokens. Line/Column are 1-based.
public sealed class Lexer(string source)
{
    private readonly string _src = source;
    private int _pos;
    private int _line = 1;
    private int _col = 1;

    public List<Diagnostic> Diagnostics { get; } = [];

    public List<Token> Tokenize()
    {
        var tokens = new List<Token>();
        while (true)
        {
            var token = Next();
            tokens.Add(token);
            if (token.Type == TokenType.Eof) break;
        }
        return tokens;
    }

    private Token Next()
    {
        SkipInlineWhitespaceAndComments();

        if (_pos >= _src.Length)
            return Make(TokenType.Eof, "", _line, _col);

        var startLine = _line;
        var startCol = _col;
        var c = _src[_pos];

        if (c is '\n' or '\r')
        {
            ConsumeNewline();
            return new Token(TokenType.Newline, "\\n", 0, startLine, startCol);
        }

        if (char.IsDigit(c))
        {
            var sb = new StringBuilder();
            while (_pos < _src.Length && char.IsDigit(_src[_pos])) sb.Append(Advance());
            var text = sb.ToString();
            // int.Parse can overflow on absurd input; clamp defensively.
            var value = int.TryParse(text, out var v) ? v : int.MaxValue;
            return new Token(TokenType.Number, text, value, startLine, startCol);
        }

        if (char.IsLetter(c))
        {
            var sb = new StringBuilder();
            while (_pos < _src.Length && char.IsLetterOrDigit(_src[_pos])) sb.Append(Advance());
            return new Token(TokenType.Ident, sb.ToString(), 0, startLine, startCol);
        }

        switch (c)
        {
            case '@': Advance(); return new Token(TokenType.At, "@", 0, startLine, startCol);
            case '+': Advance(); return new Token(TokenType.Plus, "+", 0, startLine, startCol);
            case '%': Advance(); return new Token(TokenType.Percent, "%", 0, startLine, startCol);
            case '_': Advance(); return new Token(TokenType.Underscore, "_", 0, startLine, startCol);
            case '$': Advance(); return new Token(TokenType.Dollar, "$", 0, startLine, startCol);
            case '.':
                Advance();
                if (_pos < _src.Length && _src[_pos] == '.')
                {
                    Advance();
                    return new Token(TokenType.DotDot, "..", 0, startLine, startCol);
                }
                return new Token(TokenType.Dot, ".", 0, startLine, startCol);
        }

        // Unknown character — report and skip so lexing can continue.
        Advance();
        Diagnostics.Add(new Diagnostic(startLine, startCol, $"Unexpected character '{c}'."));
        return Next();
    }

    private void SkipInlineWhitespaceAndComments()
    {
        while (_pos < _src.Length)
        {
            var c = _src[_pos];
            if (c is ' ' or '\t')
            {
                Advance();
            }
            else if (c == '#')
            {
                while (_pos < _src.Length && _src[_pos] is not ('\n' or '\r')) Advance();
            }
            else
            {
                break;
            }
        }
    }

    private void ConsumeNewline()
    {
        // Treat \r, \n and \r\n as a single newline.
        if (_src[_pos] == '\r' && _pos + 1 < _src.Length && _src[_pos + 1] == '\n') Advance();
        Advance();
        _line++;
        _col = 1;
    }

    private char Advance()
    {
        var c = _src[_pos++];
        _col++;
        return c;
    }

    private Token Make(TokenType type, string text, int line, int col) => new(type, text, 0, line, col);
}
