using System;

namespace Yafc.UI {
    public readonly struct SearchQuery {
        public readonly string query;
        public readonly string[] tokens;
        public readonly bool empty => tokens == null || tokens.Length == 0;

        public SearchQuery(string query) {
            this.query = query;
            tokens = string.IsNullOrWhiteSpace(query) ? [] : query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        }

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
}
