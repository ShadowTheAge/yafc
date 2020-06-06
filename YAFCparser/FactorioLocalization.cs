using System.Collections.Generic;
using System.IO;

namespace YAFC.Parser
{
    internal static class FactorioLocalization
    {
        private static Dictionary<string, string> keys = new Dictionary<string, string>();

        public static void Parse(Stream stream)
        {
            using (var reader = new StreamReader(stream))
            {
                var category = "";
                while (true)
                {
                    var line = reader.ReadLine();
                    if (line == null)
                        return;
                    if (line.StartsWith("[") && line.EndsWith("]"))
                        category = line.Substring(1, line.Length - 2);
                    else
                    {
                        var idx = line.IndexOf('=');
                        if (idx < 0)
                            continue;
                        var key = line.Substring(0, idx);
                        var val = line.Substring(idx + 1, line.Length - idx - 1);
                        keys[category + "." + key] = val;
                    }

                }
            }
        }
        
        public static string Localize(string key, string def = null)
        {
            if (keys.TryGetValue(key, out var val))
                return val;
            var lastDash = key.LastIndexOf('-');
            if (lastDash > 0 && int.TryParse(key.Substring(lastDash + 1), out var level) && keys.TryGetValue(key.Substring(0, lastDash), out val))
                return val + " " + level;
            return def;
        }
    }
}