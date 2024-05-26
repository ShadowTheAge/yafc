using System.Collections.Generic;
using System.IO;

namespace Yafc.Parser {
    internal static class FactorioLocalization {
        private static readonly Dictionary<string, string> keys = [];

        public static void Parse(Stream stream) {
            using StreamReader reader = new StreamReader(stream);
            string category = "";
            while (true) {
                string? line = reader.ReadLine();
                if (line == null) {
                    return;
                }

                line = line.Trim();
                if (line.StartsWith("[") && line.EndsWith("]")) {
                    category = line[1..^1];
                }
                else {
                    int idx = line.IndexOf('=');
                    if (idx < 0) {
                        continue;
                    }

                    string key = line[..idx];
                    string val = line.Substring(idx + 1, line.Length - idx - 1);
                    keys[category + "." + key] = CleanupTags(val);
                }

            }
        }

        private static string CleanupTags(string source) {
            while (true) {
                int tagStart = source.IndexOf('[');
                if (tagStart < 0) {
                    return source;
                }

                int tagEnd = source.IndexOf(']', tagStart);
                if (tagEnd < 0) {
                    return source;
                }

                source = source.Remove(tagStart, tagEnd - tagStart + 1);
            }
        }

        public static string? Localize(string key) {
            if (keys.TryGetValue(key, out string? val)) {
                return val;
            }

            int lastDash = key.LastIndexOf('-');
            if (lastDash > 0 && int.TryParse(key[(lastDash + 1)..], out int level) && keys.TryGetValue(key[..lastDash], out val)) {
                return val + " " + level;
            }

            return null;
        }
    }
}
