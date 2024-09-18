using System;

namespace Yafc.UI;

public readonly struct SearchQuery(string query) {
    public readonly string query = query;
    public readonly string[] tokens = string.IsNullOrWhiteSpace(query) ? [] : query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    public readonly bool empty => tokens == null || tokens.Length == 0;

    public bool Match(string text) {
        if (empty) {
            return true;
        }

        foreach (string token in tokens) {
            if (text.IndexOf(token, StringComparison.OrdinalIgnoreCase) < 0) {
                return false;
            }
        }

        return true;
    }
}
