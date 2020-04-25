/*using System;
using System.Collections.Generic;
using System.Numerics;
using YAFC.Model;
using YAFC.UI;

namespace YAFC
{
    public abstract class SearchableList<TData, TView> : VirtualScrollList<TData, TView> where TView : IListView<TData>, new()
    {
        protected SearchableList(Vector2 size, float elementHeight, int elementsPerRow = 1) : base(size, elementHeight, elementsPerRow) {}
        private readonly List<TData> list = new List<TData>();

        protected virtual IComparer<TData> comparer => null;

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
                    if (Filter(element))
                        list.Add(element);
                }
            } else list.AddRange(_data);

            var comparer = this.comparer;
            if (comparer != null)
                list.Sort(comparer);
            base.data = list;
        }

        protected string[] searchTokens = Array.Empty<string>();

        protected abstract bool Filter(TData obj);
    }

    public class SearchableFactorioObjectList : SearchableList<FactorioObject, FactorioObjectIconView>
    {
        public SearchableFactorioObjectList(Vector2 size) : base(size, 3f, MathUtils.Floor(size.X / 3f)) {}

        protected override bool Filter(FactorioObject obj)
        {
            foreach (var token in searchTokens)
            {   
                if (obj.name.IndexOf(token, StringComparison.OrdinalIgnoreCase) < 0 &&
                    obj.locName.IndexOf(token, StringComparison.OrdinalIgnoreCase) < 0 &&
                    (obj.locDescr == null || obj.locDescr.IndexOf(token, StringComparison.OrdinalIgnoreCase) < 0)) 
                    return false;
            }

            return true;
        }
    }
}*/