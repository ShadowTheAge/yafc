using System;
using System.Collections.Generic;
using Yafc.Model;
using Yafc.UI;

namespace Yafc {
    public class SelectSingleObjectPanel : SelectObjectPanel<FactorioObject> {
        private static readonly SelectSingleObjectPanel Instance = new SelectSingleObjectPanel();
        public SelectSingleObjectPanel() : base() { }

        public static void Select<T>(IEnumerable<T> list, string header, Action<T> select, bool allowNone = false) where T : FactorioObject => Select(list, header, select, DataUtils.DefaultOrdering, allowNone);

        public static void Select<T>(IEnumerable<T> list, string header, Action<T> select, IComparer<T> ordering, bool allowNone = false) where T : FactorioObject => Instance.Select(list, header, select, ordering, (x, selectItem) => selectItem(x), allowNone);

        protected override void NonNullElementDrawer(ImGui gui, FactorioObject element, int index) {
            if (gui.BuildFactorioObjectButton(element, display: MilestoneDisplay.Contained, extendHeader: extendHeader)) {
                CloseWithResult(element);
            }
        }
    }
}
