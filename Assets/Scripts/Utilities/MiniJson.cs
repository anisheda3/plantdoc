using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace PlantDoctor.Utilities
{
    /// <summary>
    /// Minimal dependency-free JSON reader. Needed because Unity's JsonUtility cannot
    /// deserialise dictionaries with dynamic keys (the AI "features" map).
    /// Returns Dictionary&lt;string,object&gt;, List&lt;object&gt;, string, double, bool or null.
    /// </summary>
    public static class MiniJson
    {
        public static object Parse(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            int index = 0;
            var value = ParseValue(json, ref index);
            return value;
        }

        // ---------- typed accessors (null-safe) ----------
        public static string GetString(Dictionary<string, object> o, string key, string fallback)
            => o != null && o.TryGetValue(key, out var v) && v != null ? v.ToString() : fallback;

        public static float GetFloat(Dictionary<string, object> o, string key, float fallback)
        {
            if (o == null || !o.TryGetValue(key, out var v) || v == null) return fallback;
            if (v is double d) return (float)d;
            return float.TryParse(v.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : fallback;
        }

        public static bool GetBool(Dictionary<string, object> o, string key, bool fallback)
        {
            if (o == null || !o.TryGetValue(key, out var v) || v == null) return fallback;
            if (v is bool b) return b;
            return bool.TryParse(v.ToString(), out var p) ? p : fallback;
        }

        public static float[] GetFloatArray(Dictionary<string, object> o, string key)
        {
            if (o == null || !o.TryGetValue(key, out var v) || v is not List<object> list) return null;
            var result = new float[list.Count];
            for (int i = 0; i < list.Count; i++)
                result[i] = list[i] is double d ? (float)d
                    : float.TryParse(list[i]?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : 0f;
            return result;
        }

        // ---------- parser ----------
        static object ParseValue(string s, ref int i)
        {
            SkipWhitespace(s, ref i);
            if (i >= s.Length) throw new FormatException("Unexpected end of JSON.");
            switch (s[i])
            {
                case '{': return ParseObject(s, ref i);
                case '[': return ParseArray(s, ref i);
                case '"': return ParseString(s, ref i);
                case 't': Expect(s, ref i, "true"); return true;
                case 'f': Expect(s, ref i, "false"); return false;
                case 'n': Expect(s, ref i, "null"); return null;
                default: return ParseNumber(s, ref i);
            }
        }

        static Dictionary<string, object> ParseObject(string s, ref int i)
        {
            var dict = new Dictionary<string, object>();
            i++; // '{'
            while (true)
            {
                SkipWhitespace(s, ref i);
                if (i >= s.Length) throw new FormatException("Unterminated object.");
                if (s[i] == '}') { i++; return dict; }
                if (s[i] == ',') { i++; continue; }
                string key = ParseString(s, ref i);
                SkipWhitespace(s, ref i);
                if (i >= s.Length || s[i] != ':') throw new FormatException("Expected ':' in object.");
                i++;
                dict[key] = ParseValue(s, ref i);
            }
        }

        static List<object> ParseArray(string s, ref int i)
        {
            var list = new List<object>();
            i++; // '['
            while (true)
            {
                SkipWhitespace(s, ref i);
                if (i >= s.Length) throw new FormatException("Unterminated array.");
                if (s[i] == ']') { i++; return list; }
                if (s[i] == ',') { i++; continue; }
                list.Add(ParseValue(s, ref i));
            }
        }

        static string ParseString(string s, ref int i)
        {
            SkipWhitespace(s, ref i);
            if (i >= s.Length || s[i] != '"') throw new FormatException("Expected string.");
            i++;
            var sb = new StringBuilder();
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') return sb.ToString();
                if (c != '\\') { sb.Append(c); continue; }
                if (i >= s.Length) break;
                char esc = s[i++];
                switch (esc)
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
                        if (i + 4 > s.Length) throw new FormatException("Bad unicode escape.");
                        sb.Append((char)Convert.ToInt32(s.Substring(i, 4), 16));
                        i += 4;
                        break;
                    default: sb.Append(esc); break;
                }
            }
            throw new FormatException("Unterminated string.");
        }

        static object ParseNumber(string s, ref int i)
        {
            int start = i;
            while (i < s.Length && "+-.eE0123456789".IndexOf(s[i]) >= 0) i++;
            var slice = s.Substring(start, i - start);
            if (double.TryParse(slice, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) return d;
            throw new FormatException($"Invalid number '{slice}'.");
        }

        static void Expect(string s, ref int i, string literal)
        {
            if (i + literal.Length > s.Length || s.Substring(i, literal.Length) != literal)
                throw new FormatException($"Expected '{literal}'.");
            i += literal.Length;
        }

        static void SkipWhitespace(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }
    }
}
