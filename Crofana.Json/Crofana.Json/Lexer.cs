using System.Text;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Crofana.Json
{
    internal static class Lexer
    {

        private const string NUMBER_REGEX = "^(\\+|\\-)?(\\d+(\\.\\d+)?|(\\.\\d+))((e|E)(\\+|\\-)?\\d+)?$";

        #region Private methods
        private static Token ReadConstructor(StringReader reader)
        {
            int chr;
            switch (chr = reader.Read())
            {
                case '{':
                    return new Token(TokenType.ObjectStart);
                case '}':
                    return new Token(TokenType.ObjectEnd);
                case '[':
                    return new Token(TokenType.ArrayStart);
                case ']':
                    return new Token(TokenType.ArrayEnd);
                case ':':
                    return new Token(TokenType.Colon);
                case ',':
                    return new Token(TokenType.Comma);
                default:
                    throw new JsonException("Unknown error at method 'ReadConstructor'");
            }
        }

        private static Token ReadNumber(StringReader reader)
        {
            int chr;
            var sb = new StringBuilder();
            while (true)
            {
                chr = reader.Peek();
                if (chr == '}' || chr == ']' || chr == ',' || chr == -1)
                {
                    break;
                }
                if (chr >= '0' && chr <= '9' ||
                    chr == '+' || chr == '-' ||
                    chr == '.' ||
                    chr == 'e' || chr == 'E')
                {
                    sb.Append((char) chr);
                    reader.Read();
                } else
                {
                    throw new JsonException(chr);
                }
            }
            var result = sb.ToString();
            if (Regex.IsMatch(result, NUMBER_REGEX))
            {
                return new Token(TokenType.Number, result);
            } else
            {
                throw new JsonException("Invalid number string");
            }
        }

        #region Obsolete
        /*private Token ReadNumber(StringReader reader)
        {
            int chr;
            byte flag = 0b0000; // 1.start 2.symbol 3.point 4.e
            var sb = new StringBuilder();
            while ((chr = reader.Peek()) != ',')
            {
                switch (chr)
                {
                    case int c when c >= '0' && c <= '9':
                        flag |= 0b1000;
                        break;
                    case '+':
                    case '-':
                        if ((flag >> 2 & 1) == 1 || (flag >> 3 & 1) == 1)
                        {
                            throw new JsonException("Invalid number sequence");
                        } else
                        {
                            flag |= 0b0100;
                        }
                        break;
                    case '.':
                        if ((flag >> 1 & 1) == 1)
                        {
                            throw new JsonException("Invalid number sequence");
                        } else
                        {
                            flag |= 0b0010;
                        }
                        break;
                    case 'e':
                    case 'E':
                        if ((flag & 1) == 1)
                        {
                            throw new JsonException("Invalid number sequence");
                        } else
                        {
                            flag |= 0b0001;
                            flag &= 0b0011;
                        }
                        break;
                    default:
                        throw new JsonException("Invalid number character");
                }
                sb.Append(reader.Read());
            }
        }*/
        #endregion

        private static Token ReadString(StringReader reader)
        {
            int chr;
            var sb = new StringBuilder();
            reader.Read();
            while (true)
            {
                switch (chr = reader.Read())
                {
                    case '\\':
                        sb.Append((char) chr);
                        switch (chr = reader.Read())
                        {
                            case '"':
                            case '\\':
                            case 'r':
                            case 'n':
                            case 'b':
                            case 't':
                            case 'f':
                                sb.Append((char) chr);
                                break;
                            case 'u':
                                sb.Append((char) chr);
                                for (int i = 0; i < 4; i++)
                                {
                                    chr = reader.Read();
                                    if (chr >= '0' && chr <= '9' ||
                                        chr >= 'a' && chr <= 'f' ||
                                        chr >= 'A' && chr <= 'F')
                                    {
                                        sb.Append((char) chr);
                                    } else
                                    {
                                        throw new JsonException("Invalid unicode");
                                    }
                                }
                                break;
                            default:
                                throw new JsonException("Invalid escape sequence");
                        }
                        break;
                    case '"':
                        return new Token(TokenType.String, sb.ToString());
                    case '\n':
                    case '\r':
                        throw new JsonException("\n and \r are disallowed in Json");
                    default:
                        sb.Append((char) chr);
                        break;
                }
            }
        }

        private static Token ReadConstant(StringReader reader)
        {
            int chr = reader.Read();
            switch (chr)
            {
                case 'n':
                    if (reader.Read() == 'u' && reader.Read() == 'l' && reader.Read() == 'l')
                    {
                        return new Token(TokenType.Constant, "null");
                    } else
                    {
                        throw new JsonException(chr);
                    }
                case 't':
                    if (reader.Read() == 'r' && reader.Read() == 'u' && reader.Read() == 'e')
                    {
                        return new Token(TokenType.Constant, "true");
                    } else
                    {
                        throw new JsonException(chr);
                    }
                case 'f':
                    if (reader.Read() == 'a' && reader.Read() == 'l' && reader.Read() == 's' && reader.Read() == 'e')
                    {
                        return new Token(TokenType.Constant, "false");
                    } else
                    {
                        throw new JsonException(chr);
                    }
                default:
                    throw new JsonException("Unknown error at method 'ReadConstant'");
            }
        }
        #endregion

        public static Token[] Analyze(string json)
        {
            var tokens = new List<Token>(32);
            json = json.Replace(" ", "")
                       .Replace("\t", "")
                       .Replace("\n", "")
                       .Replace("\f", "")
                       .Replace("\r", "");
            var reader = new StringReader(json);
            int chr;
            while ((chr = reader.Peek()) != -1)
            {
                switch (chr)
                {
                    case '{':
                    case '}':
                    case '[':
                    case ']':
                    case ':':
                    case ',':
                        tokens.Add(ReadConstructor(reader));
                        break;
                    case '+':
                    case '-':
                    case '.':
                    case int c when c >= '0' && c <= '9':
                        tokens.Add(ReadNumber(reader));
                        break;
                    case '"':
                        tokens.Add(ReadString(reader));
                        break;
                    case 'n':
                    case 't':
                    case 'f':
                        tokens.Add(ReadConstant(reader));
                        break;
                    default:
                        throw new JsonException(chr);
                }
            }
            tokens.Add(new Token(TokenType.End));
            return tokens.ToArray();
        }

    }

#region Obsolete
    /*internal class Lexer
    {

        private const int DEFAULT_BUFFER_LENGTH = 16;

        public ILexerState State { get; set; }
        public bool IsFinished { get; set; }
        public TextReader Reader { get; }
        public StringBuilder Buffer { get; }
        public JsonAST Ast { get; }

        public Lexer(TextReader reader) : this(reader, DEFAULT_BUFFER_LENGTH) { }

        public Lexer(TextReader reader, int bufferLength)
        {
            State = new ReadKey(this);
            Reader = reader;
            Buffer = new StringBuilder(bufferLength);
            Ast = new JsonAST();
        }

        private void Process()
        {
            State.Process();
        }

        public Token ReadToken()
        {
            var token = Ast.GetEnumerator().Current.Token;
            Ast.GetEnumerator().MoveNext();
            return token;
        }

    }*/
#endregion
}
