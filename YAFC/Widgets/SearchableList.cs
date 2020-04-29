using System;
using System.Collections.Generic;
using System.Numerics;
using YAFC.UI;

namespace YAFC
{
    public class SearchableList<TData> : VirtualScrollList<TData>
    {
        public SearchableList(float height, Vector2 elementSize, Drawer drawer, Filter filter) : base(height, elementSize, drawer)
        {
            filterFunc = filter;
        }
        private readonly List<TData> list = new List<TData>();

        public delegate bool Filter(TData data, string[] searchTokens);

        protected virtual IComparer<TData> comparer => null;
        private readonly Filter filterFunc;

        private IEnumerable<TData> _data;
        public new IEnumerable<TData> data
        {
            get => _data;
            set
            {
                _data = value;
                RefreshData();
            }
        }

        private string _filter;

        public string filter
        {
            get => _filter;
            set
            {
                _filter = value;
                searchTokens = _filter.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                RefreshData();
            }
        }

        private void RefreshData()
        {
            list.Clear();
            if (searchTokens.Length > 0)
            {
                foreach (var element in _data)
                {
                    if (filterFunc(element, searchTokens))
                        list.Add(element);
                }
            } else list.AddRange(_data);

            var comparer = this.comparer;
            if (comparer != null)
                list.Sort(comparer);
            base.data = list;
        }

        protected string[] searchTokens = Array.Empty<string>();
    }
}