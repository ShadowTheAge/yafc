using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Yafc.Model;
using Yafc.UI;

namespace Yafc {
    public class SelectMultiObjectPanel : SelectObjectPanel<IEnumerable<FactorioObject>> {
        private static readonly SelectMultiObjectPanel Instance = new SelectMultiObjectPanel();
        private readonly HashSet<FactorioObject> results = [];
        private bool allowAutoClose;
        private Predicate<FactorioObject> checkMark;

        public SelectMultiObjectPanel() : base() { }

        public static void Select<T>(IEnumerable<T> list, string header, Action<T> select, bool allowNone = false, Predicate<T> checkMark = null) where T : FactorioObject {
            Select(list, header, select, DataUtils.DefaultOrdering, allowNone, checkMark);
        }

        public static void Select<T>(IEnumerable<T> list, string header, Action<T> select, IComparer<T> ordering, bool allowNone = false, Predicate<T> checkMark = null) where T : FactorioObject {
            Instance.allowAutoClose = true;
            Instance.results.Clear();
            Instance.checkMark = (o) => checkMark?.Invoke((T)o) ?? false; // This is messy, but pushing T all the way around the call stack and type tree was messier.
            Instance.Select(list, header, select, ordering, (xs, selectItem) => {
                foreach (var x in xs ?? Enumerable.Empty<T>()) {
                    selectItem(x);
                }
            }, allowNone);
        }

        protected override void NonNullElementDrawer(ImGui gui, FactorioObject element, int index) {
            bool click = gui.BuildFactorioObjectButton(element, display: MilestoneDisplay.Contained, bgColor: results.Contains(element) ? SchemeColor.Primary : SchemeColor.None, extendHeader: extendHeader);

            if (checkMark(element)) {
                gui.DrawIcon(Rect.SideRect(gui.lastRect.TopLeft + new Vector2(1, 0), gui.lastRect.BottomRight - new Vector2(0, 1)), Icon.Check, SchemeColor.Green);
            }

            if (click) {
                if (!results.Add(element)) {
                    _ = results.Remove(element);
                }
                if (!InputSystem.Instance.control && allowAutoClose) {
                    CloseWithResult(results);
                }
                allowAutoClose = false;
            }
        }

        public override void Build(ImGui gui) {
            base.Build(gui);
            using (gui.EnterGroup(default, RectAllocator.Center)) {
                if (gui.BuildButton("OK")) {
                    CloseWithResult(results);
                }
                gui.BuildText("Hint: ctrl+click to select multiple", color: SchemeColor.BackgroundTextFaint);
            }
        }

        protected override void ReturnPressed() {
            CloseWithResult(results);
        }
    }
}
