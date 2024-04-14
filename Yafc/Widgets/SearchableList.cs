using System;
using System.Collections.Generic;
using System.Numerics;
using Yafc.UI;

namespace Yafc {
    public class SearchableList<TData> : VirtualScrollList<TData> {
        public SearchableList(float height, Vector2 elementSize, Drawer drawer, Filter filter, IComparer<TData> comparer = null) : base(height, elementSize, drawer) {
            filterFunc = filter;
            this.comparer = comparer;
        }
        private readonly List<TData> list = new List<TData>();

        public delegate bool Filter(TData data, SearchQuery searchTokens);
        private readonly IComparer<TData> comparer;
        private readonly Filter filterFunc;

        private IEnumerable<TData> _data = Array.Empty<TData>();
        public new IEnumerable<TData> data {
            get => _data;
            set {
                _data = value ?? Array.Empty<TData>();
                RefreshData();
            }
        }

        private SearchQuery _filter = default;

        public SearchQuery filter {
            get => _filter;
            set {
                _filter = value;
                RefreshData();
            }
        }

        private void RefreshData() {
            list.Clear();
            if (!_filter.empty) {
                foreach (var element in _data) {
                    if (filterFunc(element, _filter)) {
                        list.Add(element);
                    }
                }
            }
            else {
                list.AddRange(_data);
            }

            if (comparer != null) {
                list.Sort(comparer);
            }

            base.data = list;
        }
    }
}
