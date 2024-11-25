using System;
using System.Collections.Generic;
using YAFC.Model;
using YAFC.UI;

namespace YAFC
{
    public class SelectSingleObjectPanel : SelectObjectPanel<FactorioObject>
    {
        private static readonly SelectSingleObjectPanel Instance = new SelectSingleObjectPanel();
        public SelectSingleObjectPanel() : base() { }

        public static void Select<T>(IEnumerable<T> list, string header, Action<T> select, bool allowNone = false) where T : FactorioObject
            => Select(list, header, select, DataUtils.DefaultOrdering, allowNone);

        public static void Select<T>(IEnumerable<T> list, string header, Action<T> select, IComparer<T> ordering, bool allowNone = false) where T : FactorioObject
            => Instance.Select(list, header, select, ordering, allowNone, (x, selectItem) => selectItem(x));

        protected override void NonNullElementDrawer(ImGui gui, FactorioObject element, int index)
        {
            if (gui.BuildFactorioObjectButton(element, display: MilestoneDisplay.Contained, extendHeader: extendHeader) == Click.Left)
                CloseWithResult(element);
        }
    }
}