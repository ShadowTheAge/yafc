using System;
using System.Collections.Generic;
using Yafc.Model;
using Yafc.UI;

namespace Yafc {
    /// <summary>
    /// Represents a panel that can generate a result by selecting zero or one <see cref="FactorioObject"/>s. (But doesn't have to, if the user selects a close or cancel button.)
    /// </summary>
    public class SelectSingleObjectPanel : SelectObjectPanel<FactorioObject> {
        private static readonly SelectSingleObjectPanel Instance = new SelectSingleObjectPanel();
        public SelectSingleObjectPanel() : base() { }

        /// <summary>
        /// Opens a <see cref="SelectSingleObjectPanel"/> to allow the user to select one <see cref="FactorioObject"/>.
        /// </summary>
        /// <param name="list">The items to be displayed in this panel.</param>
        /// <param name="header">The string that describes to the user why they're selecting these items.</param>
        /// <param name="selectItem">An action to be called for the selected item when the panel is closed.</param>
        /// <param name="ordering">An optional ordering specifying how to sort the displayed items. If <see langword="null"/>, defaults to <see cref="DataUtils.DefaultOrdering"/>.</param>
        public static void Select<T>(IEnumerable<T> list, string header, Action<T> selectItem, IComparer<T> ordering = null) where T : FactorioObject
            => Instance.Select(list, header, selectItem, ordering, (obj, mappedAction) => mappedAction(obj), false);

        /// <summary>
        /// Opens a <see cref="SelectSingleObjectPanel"/> to allow the user to select one <see cref="FactorioObject"/>, or to clear the current selection by selecting
        /// an extra "none" or "clear" option.
        /// </summary>
        /// <param name="list">The items to be displayed in this panel.</param>
        /// <param name="header">The string that describes to the user why they're selecting these items.</param>
        /// <param name="selectItem">An action to be called for the selected item when the panel is closed. The parameter will be <see langword="null"/> if the "none" or "clear" item is selected.</param>
        /// <param name="ordering">An optional ordering specifying how to sort the displayed items. If <see langword="null"/>, defaults to <see cref="DataUtils.DefaultOrdering"/>.</param>
        public static void SelectWithNone<T>(IEnumerable<T> list, string header, Action<T> selectItem, IComparer<T> ordering = null) where T : FactorioObject
            => Instance.Select(list, header, selectItem, ordering, (obj, mappedAction) => mappedAction(obj), true);

        protected override void NonNullElementDrawer(ImGui gui, FactorioObject element, int index) {
            if (gui.BuildFactorioObjectButton(element, display: MilestoneDisplay.Contained, extendHeader: extendHeader)) {
                CloseWithResult(element);
            }
        }
    }
}
