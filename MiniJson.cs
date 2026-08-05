using System;
using System.Text;

namespace OperatorPhone.Net
{
    /// <summary>
    /// Deliberately minimal. We need maybe six string fields out of two APIs, and
    /// pulling in Newtonsoft under IL2CPP means shipping and version-matching an extra
    /// assembly in UserLibs. Not a general JSON parser — do not grow it into one. If
    /// message payloads (M2) need real parsing, add the dependency properly then.
    /// </summary>
    internal static class MiniJson
    {
        /// <summary>
        /// Finds "key": "value" at any depth. Returns null if absent.
        /// Ignores structure entirely, so a duplicate key name at a different nesting
        /// level would collide — acceptable for the fixed, known responses we parse.
        /// </summary>
        public static string GetString(string json, string key)
        {
            if (string.IsNullOrEmpty(json)) return null;

            var needle = "\"" + key + "\"";
            var i = json.IndexOf(needle, StringComparison.Ordinal);
            if (i < 0) return null;

            i += needle.Length;
            while (i < json.Length && (json[i] == ' ' || json[i] == ':')) i++;
            if (i >= json.Length || json[i] != '"') return null;
            i++;

            var sb = new StringBuilder();
            while (i < json.Length)
            {
                var c = json[i];
                if (c == '\\')
                {
                    if (++i >= json.Length) break;
                    switch (json[i])
                    {
                        case 'n': sb.Append('\n'); break;
                        case 't': sb.Append('\t'); break;
                        case 'r': sb.Append('\r'); break;
                        case 'u':
                            if (i + 4 < json.Length &&
                                ushort.TryParse(json.Substring(i + 1, 4),
                                    System.Globalization.NumberStyles.HexNumber,
                                    System.Globalization.CultureInfo.InvariantCulture, out var cp))
                            {
                                sb.Append((char)cp);
                                i += 4;
                            }
                            break;
                        default: sb.Append(json[i]); break;
                    }
                }
                else if (c == '"') break;
                else sb.Append(c);
                i++;
            }

            return sb.ToString();
        }

        public static bool GetBool(string json, string key, bool fallback = false)
        {
            if (string.IsNullOrEmpty(json)) return fallback;
            var needle = "\"" + key + "\"";
            var i = json.IndexOf(needle, StringComparison.Ordinal);
            if (i < 0) return fallback;
            i += needle.Length;
            while (i < json.Length && (json[i] == ' ' || json[i] == ':')) i++;
            return i < json.Length && json[i] == 't';
        }

        public static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length + 8);
            foreach (var c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }
    }
}
