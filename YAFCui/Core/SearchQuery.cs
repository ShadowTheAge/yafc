using System;

namespace YAFC.UI {
    public readonly struct SearchQuery {
        public readonly string query;
        public readonly string[] tokens;
        public readonly bool empty => tokens == null || tokens.Length == 0;

        public SearchQuery(string query) {
            this.query = query;
            tokens = string.IsNullOrWhiteSpace(query) ? Array.Empty<string>() : query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        }

        public bool Match(string text) {
            if (empty)
                return true;
            foreach (var token in tokens) {
                if (text.IndexOf(token, StringComparison.OrdinalIgnoreCase) < 0)
                    return false;
            }

            return true;
        }
    }
}
