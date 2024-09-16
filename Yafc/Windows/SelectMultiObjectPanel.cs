using System;
using System.Collections.Generic;
using System.Numerics;
using Yafc.Model;
using Yafc.UI;

namespace Yafc;

public class SelectMultiObjectPanel : SelectObjectPanel<IEnumerable<FactorioObject>> {
    private readonly HashSet<FactorioObject> results = [];
    private readonly Predicate<FactorioObject> checkMark;
    private bool allowAutoClose = true;

    private SelectMultiObjectPanel(Predicate<FactorioObject> checkMark) => this.checkMark = checkMark;

    /// <summary>
    /// Opens a <see cref="SelectMultiObjectPanel"/> to allow the user to select one or more <see cref="FactorioObject"/>s.
    /// </summary>
    /// <param name="list">The items to be displayed in this panel.</param>
    /// <param name="header">The string that describes to the user why they're selecting these items.</param>
    /// <param name="selectItem">An action to be called for each selected item when the panel is closed.</param>
    /// <param name="ordering">An optional ordering specifying how to sort the displayed items. If <see langword="null"/>, defaults to <see cref="DataUtils.DefaultOrdering"/>.</param>
    public static void Select<T>(IEnumerable<T> list, string header, Action<T> selectItem, IComparer<T>? ordering = null, Predicate<T>? checkMark = null) where T : FactorioObject {
        SelectMultiObjectPanel panel = new(o => checkMark?.Invoke((T)o) ?? false); // This casting is messy, but pushing T all the way around the call stack and type tree was messier.
        panel.Select(list, header, selectItem!, ordering, (objs, mappedAction) => { // null-forgiving: selectItem will not be called with null, because allowNone is false.
            foreach (var obj in objs!) { // null-forgiving: mapResult will not be called with null, because allowNone is false.
                mappedAction(obj);
            }
        }, false);
    }

    protected override void NonNullElementDrawer(ImGui gui, FactorioObject element) {
        SchemeColor bgColor = results.Contains(element) ? SchemeColor.Primary : SchemeColor.None;
        Click click = gui.BuildFactorioObjectButton(element, ButtonDisplayStyle.SelectObjectPanel(bgColor), new() { ShowTypeInHeader = showTypeInHeader });

        if (checkMark(element)) {
            gui.DrawIcon(Rect.SideRect(gui.lastRect.TopLeft + new Vector2(1, 0), gui.lastRect.BottomRight - new Vector2(0, 1)), Icon.Check, SchemeColor.Green);
        }

        if (click == Click.Left) {
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
            gui.BuildText("Hint: ctrl+click to select multiple", TextBlockDisplayStyle.HintText);
        }
    }

    protected override void ReturnPressed() => CloseWithResult(results);
}
