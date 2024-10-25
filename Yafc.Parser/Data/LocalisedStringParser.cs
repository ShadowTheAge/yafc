using System;
using System.Linq;
using System.Text;

namespace Yafc.Parser;
internal static class LocalisedStringParser {
    public static string? Parse(object localisedString) {
        try {
            return RemoveRichText(ParseStringOrArray(localisedString));
        }
        catch {
            return null;
        }
    }

    public static string? Parse(string key, object[] parameters) {
        try {
            return RemoveRichText(ParseKey(key, parameters));
        }
        catch {
            return null;
        }
    }

    private static string? ParseStringOrArray(object obj) {
        if (obj is string str) {
            return str;
        }

        if (obj is LuaTable table && table.Get(1, out string? key)) {
            return ParseKey(key, table.ArrayElements.Skip(1).ToArray()!);
        }

        return null;
    }


    private static string? ParseKey(string key, object[] parameters) {
        if (key == "") {
            var builder = new StringBuilder();
            foreach (var subString in parameters) {
                var localisedSubString = ParseStringOrArray(subString!);
                if (localisedSubString == null) {
                    return null;
                }

                builder.Append(localisedSubString);
            }

            return builder.ToString();
        }
        else if (key == "?") {
            foreach (var alternative in parameters) {
                var localisedAlternative = ParseStringOrArray(alternative!);
                if (localisedAlternative != null) {
                    return localisedAlternative;
                }
            }

            return null;
        }
        else if (FactorioLocalization.Localize(key) is { } localisedString) {
            var localisedParameters = parameters.Select(ParseStringOrArray!).ToArray();
            return ReplaceBuiltInParameters(localisedString, localisedParameters);
        }

        return null;
    }

    public static string? ReplaceBuiltInParameters(string format, string?[] parameters) {
        if (!format.Contains("__")) {
            return format;
        }

        var result = new StringBuilder();
        var cursor = 0;
        while (true) {
            var start = format.IndexOf("__", cursor);
            if (start == -1) {
                result.Append(format[cursor..]);
                return result.ToString();
            }
            if (start > cursor) {
                result.Append(format[cursor..start]);
            }

            var end = format.IndexOf("__", start + 2);
            var type = format[(start + 2)..end];
            switch (type) {
                case "CONTROL_STYLE_BEGIN":
                case "CONTROL_STYLE_END":
                case "REMARK_COLOR_BEGIN":
                case "REMARK_COLOR_END":
                    break;
                case "CONTROL_LEFT_CLICK":
                case "CONTROL_RIGHT_CLICK":
                case "CONTROL_KEY_SHIFT":
                case "CONTROL_KEY_CTRL":
                case "CONTROL_MOVE":
                    result.Append(format[start..(end + 2)]);
                    break;
                case "CONTROL":
                case "CONTROL_MODIFIER":
                case "ALT_CONTROL_LEFT_CLICK":
                case "ALT_CONTROL_RIGHT_CLICK":
                    ReadExtraParameter();
                    result.Append(format[start..(end + 2)]);
                    break;
                case "ALT_CONTROL":
                    ReadExtraParameter();
                    ReadExtraParameter();
                    result.Append(format[start..(end + 2)]);
                    break;
                case "ENTITY":
                case "ITEM":
                case "TILE":
                case "FLUID":
                    var name = ReadExtraParameter();
                    result.Append(ParseKey($"{type.ToLower()}-name.{name}", []));
                    break;
                case "plural_for_parameter":
                    var deciderIdx = ReadExtraParameter();
                    var decider = parameters[int.Parse(deciderIdx) - 1];
                    if (decider == null) {
                        return null;
                    }

                    var plurals = ReadPluralOptions();
                    var selected = SelectPluralOption(decider, plurals);
                    if (selected == null) {
                        return null;
                    }

                    var innerReplaced = ReplaceBuiltInParameters(selected, parameters);
                    if (innerReplaced == null) {
                        return null;
                    }

                    result.Append(innerReplaced);
                    break;
                default:
                    if (int.TryParse(type, out var idx) && idx >= 1 && idx <= parameters.Length) {
                        result.Append(parameters[idx - 1]);
                    }
                    else {
                        result.Append(format[start..(end + 2)]);
                    }

                    break;
            }
            cursor = end + 2;

            string ReadExtraParameter() {
                var end2 = format.IndexOf("__", end + 2);
                var result = format[(end + 2)..end2];
                end = end2;
                return result;
            }

            (Func<string, bool> Pattern, string Result)[] ReadPluralOptions() {
                var end2 = format.IndexOf("}__", end + 3);
                var options = format[(end + 3)..end2].Split('|');
                end = end2 + 1;
                return options.Select(ReadPluralOption).ToArray();
            }

            (Func<string, bool> Pattern, string Result) ReadPluralOption(string option) {
                var sides = option.Split('=');
                if (sides.Length != 2) {
                    throw new FormatException($"Invalid plural format: {option}");
                }

                var pattern = sides[0];
                var result = sides[1];
                var alternatives = pattern.Split(',');
                return (x => alternatives.Any(a => Match(a, x)), result);
            }

            string? SelectPluralOption(string decider, (Func<string, bool> Pattern, string Result)[] options) {
                foreach (var option in options) {
                    if (option.Pattern(decider)) {
                        return option.Result;
                    }
                }

                return null;
            }

            static bool Match(string pattern, string text) {
                const string ends_in_prefix = "ends in ";
                if (pattern == "rest") {
                    return true;
                }
                else if (pattern.StartsWith(ends_in_prefix)) {
                    return text.EndsWith(pattern[ends_in_prefix.Length..]);
                }
                else {
                    return text == pattern;
                }
            }
        }
    }

    private static string? RemoveRichText(string? text) {
        if (text == null) {
            return null;
        }

        var localeBuilder = new StringBuilder(text);
        _ = localeBuilder.Replace("\\n", "\n");

        // Cleaning up tags using simple state machine
        // 0 = outside of tag, 1 = first potential tag char, 2 = inside possible tag, 3 = inside definite tag
        // tag is definite when it contains '=' or starts with '/' or '.'
        int state = 0, tagStart = 0;
        for (int i = 0; i < localeBuilder.Length; i++) {
            char chr = localeBuilder[i];

            switch (state) {
                case 0:
                    if (chr == '[') {
                        state = 1;
                        tagStart = i;
                    }
                    break;
                case 1:
                    if (chr == ']') {
                        state = 0;
                    }
                    else {
                        state = (chr is '/' or '.') ? 3 : 2;
                    }

                    break;
                case 2:
                    if (chr == '=') {
                        state = 3;
                    }
                    else if (chr == ']') {
                        state = 0;
                    }

                    break;
                case 3:
                    if (chr == ']') {
                        _ = localeBuilder.Remove(tagStart, i - tagStart + 1);
                        i = tagStart - 1;
                        state = 0;
                    }
                    break;
            }
        }

        return localeBuilder.ToString();
    }
}
