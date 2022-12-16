using System;
using System.Collections.Generic;
using System.Linq;
using YAFC.Model;
using YAFC.UI;

namespace YAFC
{
    public class SelectMultiObjectPanel : SelectObjectPanel<IEnumerable<FactorioObject>>
    {
        private static readonly SelectMultiObjectPanel Instance = new SelectMultiObjectPanel();
        private readonly HashSet<FactorioObject> results = new HashSet<FactorioObject>();
        private bool allowAutoClose;

        public SelectMultiObjectPanel() : base() { }

        public static void Select<T>(IEnumerable<T> list, string header, Action<T> select, bool allowNone = false) where T : FactorioObject
            => Select(list, header, select, DataUtils.DefaultOrdering, allowNone);

        public static void Select<T>(IEnumerable<T> list, string header, Action<T> select, IComparer<T> ordering, bool allowNone = false) where T : FactorioObject
        {
            Instance.allowAutoClose = true;
            Instance.results.Clear();
            Instance.Select(list, header, select, ordering, allowNone, (xs, selectItem) =>
            {
                foreach (var x in xs ?? Enumerable.Empty<T>())
                    selectItem(x);
            });
        }

        protected override void NonNullElementDrawer(ImGui gui, FactorioObject element, int index)
        {
            if (gui.BuildFactorioObjectButton(element, display: MilestoneDisplay.Contained, bgColor: results.Contains(element) ? SchemeColor.Primary : SchemeColor.None, extendHeader: extendHeader) == Click.Left)
            {
                if (!results.Add(element))
                    results.Remove(element);
                if (!InputSystem.Instance.control && allowAutoClose)
                    CloseWithResult(results);
                allowAutoClose = false;
            }
        }

        public override void Build(ImGui gui)
        {
            base.Build(gui);
            using (gui.EnterGroup(default, RectAllocator.Center))
            {
                if (gui.BuildButton("OK"))
                    CloseWithResult(results);
                gui.BuildText("Hint: ctrl+click to select multiple", color: SchemeColor.BackgroundTextFaint);
            }
        }

        protected override void ReturnPressed()
        {
            CloseWithResult(results);
        }
    }
}