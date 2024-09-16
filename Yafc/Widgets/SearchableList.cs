using System.Collections.Generic;
using System.Numerics;
using Yafc.UI;

namespace Yafc;

public class SearchableList<TData>(float height, Vector2 elementSize, VirtualScrollList<TData>.Drawer drawer, SearchableList<TData>.Filter filter, IComparer<TData>? comparer = null)
    : VirtualScrollList<TData>(height, elementSize, drawer) {

    private readonly List<TData> list = [];

    public delegate bool Filter(TData data, SearchQuery searchTokens);

    private readonly Filter filterFunc = filter;

    private IEnumerable<TData> _data = [];
    // TODO (https://github.com/shpaass/yafc-ce/issues/293) investigate set()
    public new IEnumerable<TData> data {
        get => _data;
        set {
            _data = value ?? [];
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
