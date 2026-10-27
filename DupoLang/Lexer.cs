using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace DupoLang
{
    public enum TokType { Number, String, Ident, Keyword, Op, EOF }

    public class Token
    {
        public TokType Type;
        public string Text;
        public double Num;
        public int Line;
        public override string ToString() { return Type + "(" + Text + ")"; }
    }

    /// <summary>Ошибка выполнения или разбора DupoLang.</summary>
    public class DupoError : Exception
    {
        public int Line;
        public DupoError(string message, int line) : base(message) { Line = line; }
        public override string ToString()
        {
            return Line > 0 ? ("Строка " + Line + ": " + Message) : Message;
        }
    }

    public class Lexer
    {
        public static readonly HashSet<string> Keywords = new HashSet<string>(StringComparer.Ordinal)
        {
            "import", "using", "var", "new", "func", "return", "if", "else",
            "while", "for", "true", "false", "null", "break", "continue"
        };

        static readonly string[] Ops2 = { "==", "!=", "<=", ">=", "&&", "||", "+=", "-=", "*=", "/=", "++", "--" };
        const string Ops1 = "+-*/%=<>!(){}[],.;:?";

        readonly string src;
        int pos;
        int line = 1;

        public Lexer(string source) { src = source ?? string.Empty; }

        char Cur { get { return pos < src.Length ? src[pos] : '\0'; } }
        char Nxt { get { return pos + 1 < src.Length ? src[pos + 1] : '\0'; } }

        public List<Token> Tokenize()
        {
            var result = new List<Token>();
            while (true)
            {
                SkipTrivia();
                if (pos >= src.Length) { result.Add(Make(TokType.EOF, "<конец>")); break; }
                char c = Cur;
                if (char.IsDigit(c)) { result.Add(ReadNumber()); continue; }
                if (c == '"' || c == '\'') { result.Add(ReadString(c)); continue; }
                if (char.IsLetter(c) || c == '_') { result.Add(ReadIdent()); continue; }
                result.Add(ReadOperator());
            }
            return result;
        }

        Token Make(TokType t, string text)
        {
            return new Token { Type = t, Text = text, Line = line };
        }

        void SkipTrivia()
        {
            while (pos < src.Length)
            {
                char c = src[pos];
                if (c == '\n') { line++; pos++; }
                else if (c == ' ' || c == '\t' || c == '\r') pos++;
                else if (c == '/' && Nxt == '/') { while (pos < src.Length && src[pos] != '\n') pos++; }
                else if (c == '#') { while (pos < src.Length && src[pos] != '\n') pos++; }
                else if (c == '/' && Nxt == '*')
                {
                    pos += 2;
                    while (pos + 1 < src.Length && !(src[pos] == '*' && src[pos + 1] == '/'))
                    {
                        if (src[pos] == '\n') line++;
                        pos++;
                    }
                    pos = Math.Min(pos + 2, src.Length);
                }
                else break;
            }
        }

        Token ReadNumber()
        {
            int start = pos;
            if (Cur == '0' && (Nxt == 'x' || Nxt == 'X'))
            {
                pos += 2;
                int hs = pos;
                while (pos < src.Length && Uri.IsHexDigit(src[pos])) pos++;
                var tk = Make(TokType.Number, src.Substring(start, pos - start));
                tk.Num = (double)Convert.ToInt64(src.Substring(hs, pos - hs), 16);
                return tk;
            }
            while (pos < src.Length && char.IsDigit(src[pos])) pos++;
            if (pos < src.Length && src[pos] == '.' && pos + 1 < src.Length && char.IsDigit(src[pos + 1]))
            {
                pos++;
                while (pos < src.Length && char.IsDigit(src[pos])) pos++;
            }
            string text = src.Substring(start, pos - start);
            var t = Make(TokType.Number, text);
            t.Num = double.Parse(text, CultureInfo.InvariantCulture);
            return t;
        }

        Token ReadString(char quote)
        {
            int startLine = line;
            pos++;
            var sb = new StringBuilder();
            while (true)
            {
                if (pos >= src.Length) throw new DupoError("Не закрыта кавычка в строке", startLine);
                char c = src[pos];
                if (c == quote) { pos++; break; }
                if (c == '\n') { line++; sb.Append('\n'); pos++; continue; }
                if (c == '\\' && pos + 1 < src.Length)
                {
                    char e = src[pos + 1];
                    pos += 2;
                    if (e == 'n') sb.Append('\n');
                    else if (e == 't') sb.Append('\t');
                    else if (e == 'r') sb.Append('\r');
                    else if (e == '0') sb.Append('\0');
                    else if (e == 'u' && pos + 3 < src.Length)
                    {
                        sb.Append((char)Convert.ToInt32(src.Substring(pos, 4), 16));
                        pos += 4;
                    }
                    else sb.Append(e);
                    continue;
                }
                sb.Append(c);
                pos++;
            }
            var t = Make(TokType.String, sb.ToString());
            t.Line = startLine;
            return t;
        }

        Token ReadIdent()
        {
            int start = pos;
            while (pos < src.Length && (char.IsLetterOrDigit(src[pos]) || src[pos] == '_')) pos++;
            string text = src.Substring(start, pos - start);
            return Make(Keywords.Contains(text) ? TokType.Keyword : TokType.Ident, text);
        }

        Token ReadOperator()
        {
            if (pos + 1 < src.Length)
            {
                string two = src.Substring(pos, 2);
                for (int i = 0; i < Ops2.Length; i++)
                {
                    if (Ops2[i] == two) { pos += 2; return Make(TokType.Op, two); }
                }
            }
            char c = Cur;
            if (Ops1.IndexOf(c) < 0) throw new DupoError("Непонятный символ '" + c + "'", line);
            pos++;
            return Make(TokType.Op, c.ToString());
        }
    }
}
