using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Pvz.Runtime.Remote
{
    /// <summary>
    /// The text protocol of the debug remote (T026): one command per line («give BOX-001»), one JSON object per reply.
    /// Pure helpers — no sockets, no scene — so they are tested in the editor.
    /// </summary>
    public static class RemoteProtocol
    {
        /// <summary>Splits a command line into a lower-case name and its arguments (extra spaces are ignored).</summary>
        public static bool TryParse(string line, out string name, out string[] args)
        {
            name = null;
            args = Array.Empty<string>();
            if (string.IsNullOrWhiteSpace(line)) return false;
            var parts = line.Trim().Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            name = parts[0].ToLowerInvariant();
            args = new string[parts.Length - 1];
            Array.Copy(parts, 1, args, 0, args.Length);
            return true;
        }

        /// <summary>Reads a number written with a decimal point, whatever the device language.</summary>
        public static bool TryNumber(string text, out float value) =>
            float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

        public static string Error(string message) => new Json().Add("ok", false).Add("error", message).ToString();

        public static string Ok(string key = null, object value = null)
        {
            var json = new Json().Add("ok", true);
            if (key != null) json.Add(key, value);
            return json.ToString();
        }
    }

    /// <summary>A tiny JSON object writer (strings escaped, numbers with a decimal point).</summary>
    public sealed class Json
    {
        private readonly StringBuilder _text = new StringBuilder("{");
        private bool _first = true;

        public Json Add(string key, object value)
        {
            if (!_first) _text.Append(',');
            _first = false;
            _text.Append(Quote(key)).Append(':').Append(Value(value));
            return this;
        }

        public override string ToString() => _text.ToString() + "}";

        /// <summary>A raw, already written JSON value (nested object or array).</summary>
        public sealed class Raw
        {
            public Raw(string text) => Text = text;
            public string Text { get; }
        }

        public static string Array(IEnumerable<string> rawItems) => "[" + string.Join(",", rawItems) + "]";

        public static string Value(object value)
        {
            switch (value)
            {
                case null: return "null";
                case Raw raw: return raw.Text;
                case bool b: return b ? "true" : "false";
                case string s: return Quote(s);
                case float f: return float.IsNaN(f) || float.IsInfinity(f) ? "null" : f.ToString("0.###", CultureInfo.InvariantCulture);
                case double d: return double.IsNaN(d) || double.IsInfinity(d) ? "null" : d.ToString("0.###", CultureInfo.InvariantCulture);
                case int i: return i.ToString(CultureInfo.InvariantCulture);
                case long l: return l.ToString(CultureInfo.InvariantCulture);
                default: return Quote(Convert.ToString(value, CultureInfo.InvariantCulture));
            }
        }

        public static string Quote(string s)
        {
            var text = new StringBuilder("\"");
            foreach (var c in s ?? string.Empty)
            {
                switch (c)
                {
                    case '"': text.Append("\\\""); break;
                    case '\\': text.Append("\\\\"); break;
                    case '\n': text.Append("\\n"); break;
                    case '\r': text.Append("\\r"); break;
                    case '\t': text.Append("\\t"); break;
                    default:
                        if (c < 0x20) text.Append("\\u").Append(((int)c).ToString("x4"));
                        else text.Append(c);
                        break;
                }
            }
            return text.Append('"').ToString();
        }
    }
}
