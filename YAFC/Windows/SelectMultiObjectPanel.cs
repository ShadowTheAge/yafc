using System;
using System.Collections.Generic;
using System.Linq;
using YAFC.Model;
using YAFC.UI;

namespace YAFC {
    public class SelectMultiObjectPanel : SelectObjectPanel<IEnumerable<FactorioObject>> {
        private static readonly SelectMultiObjectPanel Instance = new SelectMultiObjectPanel();
        private readonly HashSet<FactorioObject> results = new HashSet<FactorioObject>();
        public SelectMultiObjectPanel() : base() { }

        public static void Select<T>(IEnumerable<T> list, string header, Action<T> select, bool allowNone = false) where T : FactorioObject {
            Select(list, header, select, DataUtils.DefaultOrdering, allowNone);
        }

        public static void Select<T>(IEnumerable<T> list, string header, Action<T> select, IComparer<T> ordering, bool allowNone = false) where T : FactorioObject {
            Instance.results.Clear();
            Instance.Select(list, header, select, ordering, (xs, selectItem) => {
                foreach (var x in xs ?? Enumerable.Empty<T>()) {
                    selectItem(x);
                }
            }, allowNone);
        }

        protected override void NonNullElementDrawer(ImGui gui, FactorioObject element, int index) {
            if (gui.BuildFactorioObjectButton(element, display: MilestoneDisplay.Contained, bgColor: results.Contains(element) ? SchemeColor.Primary : SchemeColor.None, extendHeader: extendHeader)) {
                if (!results.Add(element)) {
                    results.Remove(element);
                }
                if (!InputSystem.Instance.control) {
                    CloseWithResult(results);
                }
            }
        }
    }
}
