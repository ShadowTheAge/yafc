using System.Collections.Generic;
using System.IO;

namespace YAFC.Parser {
    internal static class FactorioLocalization {
        private static Dictionary<string, string> keys = new Dictionary<string, string>();

        public static void Parse(Stream stream) {
            using (var reader = new StreamReader(stream)) {
                var category = "";
                while (true) {
                    var line = reader.ReadLine();
                    if (line == null)
                        return;
                    line = line.Trim();
                    if (line.StartsWith("[") && line.EndsWith("]"))
                        category = line.Substring(1, line.Length - 2);
                    else {
                        var idx = line.IndexOf('=');
                        if (idx < 0)
                            continue;
                        var key = line.Substring(0, idx);
                        var val = line.Substring(idx + 1, line.Length - idx - 1);
                        keys[category + "." + key] = CleanupTags(val);
                    }

                }
            }
        }

        private static string CleanupTags(string source) {
            while (true) {
                var tagStart = source.IndexOf('[');
                if (tagStart < 0)
                    return source;
                var tagEnd = source.IndexOf(']', tagStart);
                if (tagEnd < 0)
                    return source;
                source = source.Remove(tagStart, tagEnd - tagStart + 1);
            }
        }

        public static string Localize(string key) {
            if (keys.TryGetValue(key, out var val))
                return val;
            var lastDash = key.LastIndexOf('-');
            if (lastDash > 0 && int.TryParse(key.Substring(lastDash + 1), out var level) && keys.TryGetValue(key.Substring(0, lastDash), out val))
                return val + " " + level;
            return null;
        }
    }
}
