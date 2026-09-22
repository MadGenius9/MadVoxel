using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace MadVoxel.Modding
{
    public enum JsonKind
    {
        Null,
        Bool,
        Number,
        String,
        Array,
        Object
    }

    /// <summary>
    /// A parsed JSON node.
    ///
    /// Unity's JsonUtility cannot tell "field absent" from "field set to its default",
    /// which is exactly the distinction a patching mod system lives on, and it cannot
    /// represent dictionaries. So the mod loader parses to this tree instead, which also
    /// lets every error carry the line it came from.
    /// </summary>
    public sealed class JsonValue
    {
        public JsonKind Kind;
        public bool Bool;
        public double Number;
        public string String;
        public List<JsonValue> Array;
        public Dictionary<string, JsonValue> Object;
        public int Line;

        public bool IsNull { get { return Kind == JsonKind.Null; } }

        public int Count
        {
            get
            {
                if (Kind == JsonKind.Array) return Array.Count;
                if (Kind == JsonKind.Object) return Object.Count;
                return 0;
            }
        }

        public JsonValue this[string key]
        {
            get
            {
                JsonValue value;
                if (Kind == JsonKind.Object && Object.TryGetValue(key, out value)) return value;
                return null;
            }
        }

        public JsonValue this[int index]
        {
            get
            {
                if (Kind != JsonKind.Array || index < 0 || index >= Array.Count) return null;
                return Array[index];
            }
        }

        public bool Has(string key)
        {
            return Kind == JsonKind.Object && Object.ContainsKey(key);
        }

        public override string ToString()
        {
            switch (Kind)
            {
                case JsonKind.Null: return "null";
                case JsonKind.Bool: return Bool ? "true" : "false";
                case JsonKind.Number: return Number.ToString(CultureInfo.InvariantCulture);
                case JsonKind.String: return String;
                case JsonKind.Array: return "[" + Array.Count + " items]";
                default: return "{" + Object.Count + " fields}";
            }
        }
    }

    /// <summary>
    /// A small strict JSON reader. Allows // and /* */ comments and trailing commas,
    /// because hand-written mod files get both, and rejects everything else loudly with
    /// a line number.
    /// </summary>
    public static class Json
    {
        public static JsonValue Parse(string text, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(text))
            {
                error = "the file is empty";
                return null;
            }

            int index = 0;
            int line = 1;

            try
            {
                SkipTrivia(text, ref index, ref line);
                var value = ParseValue(text, ref index, ref line);
                SkipTrivia(text, ref index, ref line);

                if (index < text.Length)
                {
                    error = string.Format("line {0}: unexpected '{1}' after the end of the document", line, text[index]);
                    return null;
                }
                return value;
            }
            catch (JsonException ex)
            {
                error = ex.Message;
                return null;
            }
        }

        class JsonException : System.Exception
        {
            public JsonException(string message) : base(message) { }
        }

        static JsonException Fail(int line, string message)
        {
            return new JsonException(string.Format("line {0}: {1}", line, message));
        }

        static void SkipTrivia(string text, ref int i, ref int line)
        {
            while (i < text.Length)
            {
                char c = text[i];

                if (c == '\n') { line++; i++; continue; }
                if (c == ' ' || c == '\t' || c == '\r') { i++; continue; }

                // Comments are not standard JSON, but mod files are written by hand.
                if (c == '/' && i + 1 < text.Length)
                {
                    if (text[i + 1] == '/')
                    {
                        while (i < text.Length && text[i] != '\n') i++;
                        continue;
                    }
                    if (text[i + 1] == '*')
                    {
                        int start = line;
                        i += 2;
                        while (true)
                        {
                            if (i + 1 >= text.Length) throw Fail(start, "block comment is never closed");
                            if (text[i] == '\n') line++;
                            if (text[i] == '*' && text[i + 1] == '/') { i += 2; break; }
                            i++;
                        }
                        continue;
                    }
                }
                return;
            }
        }

        static JsonValue ParseValue(string text, ref int i, ref int line)
        {
            if (i >= text.Length) throw Fail(line, "the document ends where a value was expected");

            char c = text[i];
            switch (c)
            {
                case '{': return ParseObject(text, ref i, ref line);
                case '[': return ParseArray(text, ref i, ref line);
                case '"': return new JsonValue { Kind = JsonKind.String, String = ParseString(text, ref i, ref line), Line = line };
            }

            if (Matches(text, i, "true")) { i += 4; return new JsonValue { Kind = JsonKind.Bool, Bool = true, Line = line }; }
            if (Matches(text, i, "false")) { i += 5; return new JsonValue { Kind = JsonKind.Bool, Bool = false, Line = line }; }
            if (Matches(text, i, "null")) { i += 4; return new JsonValue { Kind = JsonKind.Null, Line = line }; }

            return ParseNumber(text, ref i, line);
        }

        static bool Matches(string text, int i, string word)
        {
            if (i + word.Length > text.Length) return false;
            for (int k = 0; k < word.Length; k++)
            {
                if (text[i + k] != word[k]) return false;
            }
            return true;
        }

        static JsonValue ParseObject(string text, ref int i, ref int line)
        {
            int startLine = line;
            var result = new JsonValue
            {
                Kind = JsonKind.Object,
                Object = new Dictionary<string, JsonValue>(),
                Line = startLine
            };

            i++; // '{'
            SkipTrivia(text, ref i, ref line);

            if (i < text.Length && text[i] == '}') { i++; return result; }

            while (true)
            {
                SkipTrivia(text, ref i, ref line);
                if (i < text.Length && text[i] == '}') { i++; return result; } // trailing comma

                if (i >= text.Length || text[i] != '"') throw Fail(line, "expected a quoted field name");

                int keyLine = line;
                string key = ParseString(text, ref i, ref line);
                if (result.Object.ContainsKey(key)) throw Fail(keyLine, "duplicate field '" + key + "'");

                SkipTrivia(text, ref i, ref line);
                if (i >= text.Length || text[i] != ':') throw Fail(line, "expected ':' after field '" + key + "'");
                i++;

                SkipTrivia(text, ref i, ref line);
                result.Object[key] = ParseValue(text, ref i, ref line);

                SkipTrivia(text, ref i, ref line);
                if (i >= text.Length) throw Fail(startLine, "object is never closed");

                if (text[i] == ',') { i++; continue; }
                if (text[i] == '}') { i++; return result; }
                throw Fail(line, "expected ',' or '}' after the value of '" + key + "'");
            }
        }

        static JsonValue ParseArray(string text, ref int i, ref int line)
        {
            int startLine = line;
            var result = new JsonValue
            {
                Kind = JsonKind.Array,
                Array = new List<JsonValue>(),
                Line = startLine
            };

            i++; // '['
            SkipTrivia(text, ref i, ref line);
            if (i < text.Length && text[i] == ']') { i++; return result; }

            while (true)
            {
                SkipTrivia(text, ref i, ref line);
                if (i < text.Length && text[i] == ']') { i++; return result; } // trailing comma

                result.Array.Add(ParseValue(text, ref i, ref line));

                SkipTrivia(text, ref i, ref line);
                if (i >= text.Length) throw Fail(startLine, "array is never closed");

                if (text[i] == ',') { i++; continue; }
                if (text[i] == ']') { i++; return result; }
                throw Fail(line, "expected ',' or ']' in array");
            }
        }

        static string ParseString(string text, ref int i, ref int line)
        {
            int startLine = line;
            var sb = new StringBuilder();
            i++; // opening quote

            while (true)
            {
                if (i >= text.Length) throw Fail(startLine, "string is never closed");

                char c = text[i];
                if (c == '"') { i++; return sb.ToString(); }

                if (c == '\n') throw Fail(startLine, "string is never closed");

                if (c != '\\') { sb.Append(c); i++; continue; }

                i++;
                if (i >= text.Length) throw Fail(startLine, "string ends in an escape");

                char e = text[i];
                i++;
                switch (e)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                    {
                        if (i + 4 > text.Length) throw Fail(line, "truncated \\u escape");
                        int code = 0;
                        for (int k = 0; k < 4; k++)
                        {
                            int digit = HexDigit(text[i + k]);
                            if (digit < 0) throw Fail(line, "bad \\u escape");
                            code = code * 16 + digit;
                        }
                        sb.Append((char)code);
                        i += 4;
                        break;
                    }
                    default:
                        throw Fail(line, "unknown escape '\\" + e + "'");
                }
            }
        }

        static int HexDigit(char c)
        {
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'a' && c <= 'f') return c - 'a' + 10;
            if (c >= 'A' && c <= 'F') return c - 'A' + 10;
            return -1;
        }

        static JsonValue ParseNumber(string text, ref int i, int line)
        {
            int start = i;
            if (i < text.Length && (text[i] == '-' || text[i] == '+')) i++;

            bool any = false;
            while (i < text.Length && (char.IsDigit(text[i]) || text[i] == '.' || text[i] == 'e' || text[i] == 'E'
                                       || ((text[i] == '-' || text[i] == '+') && (text[i - 1] == 'e' || text[i - 1] == 'E'))))
            {
                if (char.IsDigit(text[i])) any = true;
                i++;
            }

            if (!any) throw Fail(line, "expected a value");

            string raw = text.Substring(start, i - start);
            double number;
            if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out number))
                throw Fail(line, "'" + raw + "' is not a number");

            return new JsonValue { Kind = JsonKind.Number, Number = number, Line = line };
        }
    }
}
