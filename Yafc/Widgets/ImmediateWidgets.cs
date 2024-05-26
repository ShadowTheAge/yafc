using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using SDL2;
using Yafc.Model;
using Yafc.UI;

namespace Yafc {
    public enum MilestoneDisplay {
        Normal,
        Contained,
        All,
        AllContained,
        None
    }

    public enum GoodsWithAmountEvent {
        None,
        ButtonClick,
        TextEditing,
    }

    public static class ImmediateWidgets {
        public static void BuildFactorioObjectIcon(this ImGui gui, FactorioObject obj, MilestoneDisplay display = MilestoneDisplay.Normal, float size = 2f) {
            if (obj == null) {
                gui.BuildIcon(Icon.Empty, size, SchemeColor.BackgroundTextFaint);
                return;
            }

            var color = obj.IsAccessible() ? SchemeColor.Source : SchemeColor.SourceFaint;
            gui.BuildIcon(obj.icon, size, color);
            if (gui.isBuilding && display != MilestoneDisplay.None) {
                bool contain = (display & MilestoneDisplay.Contained) != 0;
                var milestone = Milestones.Instance.GetHighest(obj, display >= MilestoneDisplay.All);
                if (milestone != null) {
                    Vector2 psize = new Vector2(size / 2f);
                    var delta = contain ? psize : psize / 2f;
                    Rect milestoneIcon = new Rect(gui.lastRect.BottomRight - delta, psize);
                    var icon = milestone == Database.voidEnergy ? DataUtils.HandIcon : milestone.icon;
                    gui.DrawIcon(milestoneIcon, icon, color);
                }
            }
        }

        public static bool BuildFloatInput(this ImGui gui, float value, out float newValue, UnitOfMeasure unit, Padding padding) {
            if (gui.BuildTextInput(DataUtils.FormatAmount(value, unit), out string newText, null, Icon.None, true, padding) && DataUtils.TryParseAmount(newText, out newValue, unit)) {
                return true;
            }

            newValue = value;
            return false;
        }

        public static bool BuildFactorioObjectButton(this ImGui gui, Rect rect, FactorioObject obj, SchemeColor bgColor = SchemeColor.None, bool extendHeader = false) {
            SchemeColor overColor;
            if (bgColor == SchemeColor.None) {
                overColor = SchemeColor.Grey;
            }
            else {
                overColor = bgColor + 1;
            }
            if (MainScreen.Instance.IsSameObjectHovered(gui, obj)) {
                bgColor = overColor;
            }
            var evt = gui.BuildButton(rect, bgColor, overColor, button: 0);
            if (evt == ButtonEvent.MouseOver && obj != null) {
                MainScreen.Instance.ShowTooltip(obj, gui, rect, extendHeader);
            }
            else if (evt == ButtonEvent.Click) {
                if (gui.actionParameter == SDL.SDL_BUTTON_MIDDLE && obj != null) {
                    if (obj is Goods goods && obj.IsAccessible()) {
                        NeverEnoughItemsPanel.Show(goods);
                    }
                    else {
                        DependencyExplorer.Show(obj);
                    }
                }
                else if (gui.actionParameter == SDL.SDL_BUTTON_LEFT) {
                    return true;
                }
            }

            return false;
        }

        public static bool BuildFactorioObjectButton(this ImGui gui, FactorioObject obj, float size = 2f, MilestoneDisplay display = MilestoneDisplay.Normal, SchemeColor bgColor = SchemeColor.None, bool extendHeader = false) {
            gui.BuildFactorioObjectIcon(obj, display, size);
            return gui.BuildFactorioObjectButton(gui.lastRect, obj, bgColor, extendHeader);
        }

        public static bool BuildFactorioObjectButtonWithText(this ImGui gui, FactorioObject obj, string extraText = null, float size = 2f, MilestoneDisplay display = MilestoneDisplay.Normal) {
            using (gui.EnterRow()) {
                gui.BuildFactorioObjectIcon(obj, display, size);
                var color = gui.textColor;
                if (obj != null && !obj.IsAccessible()) {
                    color += 1;
                }

                if (Project.current.preferences.favorites.Contains(obj)) {
                    gui.BuildIcon(Icon.StarFull, 1f);
                }

                if (extraText != null) {
                    gui.AllocateSpacing();
                    gui.allocator = RectAllocator.RightRow;
                    gui.BuildText(extraText, color: color);
                }
                _ = gui.RemainingRow();
                gui.BuildText(obj == null ? "None" : obj.locName, wrap: true, color: color);
            }

            return gui.BuildFactorioObjectButton(gui.lastRect, obj);
        }

        public static bool BuildInlineObjectList<T>(this ImGui gui, IEnumerable<T> list, IComparer<T> ordering, string header, out T selected, int maxCount = 10,
            Predicate<T> checkMark = null, Func<T, string> extra = null) where T : FactorioObject {
            gui.BuildText(header, Font.productionTableHeader);
            IEnumerable<T> sortedList;
            if (ordering == DataUtils.AlreadySortedRecipe) {
                sortedList = list.AsEnumerable();
            }
            else {
                sortedList = list.OrderBy(e => e, ordering ?? DataUtils.DefaultOrdering);
            }
            selected = null;
            foreach (var elem in sortedList.Take(maxCount)) {
                string extraText = extra?.Invoke(elem);
                if (gui.BuildFactorioObjectButtonWithText(elem, extraText)) {
                    selected = elem;
                }

                if (checkMark != null && gui.isBuilding && checkMark(elem)) {
                    gui.DrawIcon(Rect.Square(new Vector2(gui.lastRect.Right - 1f, gui.lastRect.Center.Y), 1.5f), Icon.Check, SchemeColor.Green);
                }
            }

            return selected != null;
        }

        public static void BuildInlineObjectListAndButton<T>(this ImGui gui, ICollection<T> list, IComparer<T> ordering, Action<T> selectItem, string header, int count = 6, bool multiple = false, Predicate<T> checkMark = null, Func<T, string> extra = null) where T : FactorioObject {
            using (gui.EnterGroup(default, RectAllocator.Stretch)) {
                if (gui.BuildInlineObjectList(list, ordering, header, out var selected, count, checkMark, extra)) {
                    selectItem(selected);
                    if (!multiple || !InputSystem.Instance.control) {
                        _ = gui.CloseDropdown();
                    }
                }

                if (list.Count > count && gui.BuildButton("See full list") && gui.CloseDropdown()) {
                    if (multiple) {
                        SelectMultiObjectPanel.Select(list, header, selectItem, ordering, checkMark);
                    }
                    else {
                        SelectSingleObjectPanel.Select(list, header, selectItem, ordering);
                    }
                }
            }
        }

        public static void BuildInlineObjectListAndButtonWithNone<T>(this ImGui gui, ICollection<T> list, IComparer<T> ordering, Action<T> selectItem, string header, int count = 6, Func<T, string> extra = null) where T : FactorioObject {
            using (gui.EnterGroup(default, RectAllocator.Stretch)) {
                if (gui.BuildInlineObjectList(list, ordering, header, out var selected, count, null, extra)) {
                    selectItem(selected);
                    _ = gui.CloseDropdown();
                }
                if (gui.BuildRedButton("Clear") && gui.CloseDropdown()) {
                    selectItem(null);
                }

                if (list.Count > count && gui.BuildButton("See full list") && gui.CloseDropdown()) {
                    SelectSingleObjectPanel.SelectWithNone(list, header, selectItem, ordering);
                }
            }
        }

        public static bool BuildFactorioObjectWithAmount(this ImGui gui, FactorioObject goods, float amount, UnitOfMeasure unit, SchemeColor bgColor = SchemeColor.None, SchemeColor textColor = SchemeColor.None) {
            using (gui.EnterFixedPositioning(3f, 3f, default)) {
                gui.allocator = RectAllocator.Stretch;
                gui.spacing = 0f;
                bool clicked = gui.BuildFactorioObjectButton(goods, 3f, MilestoneDisplay.Contained, bgColor);
                if (goods != null) {
                    gui.BuildText(DataUtils.FormatAmount(amount, unit), Font.text, false, RectAlignment.Middle, textColor);
                    if (InputSystem.Instance.control && gui.BuildButton(gui.lastRect, SchemeColor.None, SchemeColor.Grey) == ButtonEvent.MouseOver) {
                        ShowPrecisionValueTooltip(gui, amount, unit, goods);
                    }
                }
                return clicked;
            }
        }

        public static void ShowPrecisionValueTooltip(ImGui gui, float amount, UnitOfMeasure unit, FactorioObject goods) {
            string text;
            switch (unit) {
                case UnitOfMeasure.PerSecond:
                case UnitOfMeasure.FluidPerSecond:
                case UnitOfMeasure.ItemPerSecond:
                    string perSecond = DataUtils.FormatAmountRaw(amount, 1f, "/s", DataUtils.PreciseFormat);
                    string perMinute = DataUtils.FormatAmountRaw(amount, 60f, "/m", DataUtils.PreciseFormat);
                    string perHour = DataUtils.FormatAmountRaw(amount, 3600f, "/h", DataUtils.PreciseFormat);
                    text = perSecond + "\n" + perMinute + "\n" + perHour;
                    if (goods is Item item) {
                        text += DataUtils.FormatAmount(MathF.Abs(item.stackSize / amount), UnitOfMeasure.Second, "\n", " per stack");
                    }

                    break;
                default:
                    text = DataUtils.FormatAmount(amount, unit, precise: true);
                    break;
            }
            gui.ShowTooltip(gui.lastRect, x => {
                _ = x.BuildFactorioObjectButtonWithText(goods);
                x.BuildText(text, wrap: true);
            }, 10f);
        }

        /// <summary>Shows a dropdown containing the (partial) <paramref name="list"/> of elements, with an action for when an element is selected.</summary>
        /// <param name="count">Maximum number of elements in the list. If there are more another popup can be opened by the user to show the full list.</param>
        /// <param name="width">Width of the popup. Make sure the header text fits!</param>
        public static void BuildObjectSelectDropDown<T>(this ImGui gui, ICollection<T> list, IComparer<T> ordering, Action<T> selectItem, string header, float width = 20f, int count = 6, bool multiple = false, Predicate<T> checkMark = null, Func<T, string> extra = null) where T : FactorioObject
            => gui.ShowDropDown(imGui => imGui.BuildInlineObjectListAndButton(list, ordering, selectItem, header, count, multiple, checkMark, extra), width);

        /// <summary>Shows a dropdown containing the (partial) <paramref name="list"/> of elements, with an action for when an element is selected. An additional "Clear" or "None" option will also be displayed.</summary>
        /// <param name="count">Maximum number of elements in the list. If there are more another popup can be opened by the user to show the full list.</param>
        /// <param name="width">Width of the popup. Make sure the header text fits!</param>
        public static void BuildObjectSelectDropDownWithNone<T>(this ImGui gui, ICollection<T> list, IComparer<T> ordering, Action<T> selectItem, string header, float width = 20f, int count = 6, Func<T, string> extra = null) where T : FactorioObject
            => gui.ShowDropDown(imGui => imGui.BuildInlineObjectListAndButtonWithNone(list, ordering, selectItem, header, count, extra), width);

        public static GoodsWithAmountEvent BuildFactorioObjectWithEditableAmount(this ImGui gui, FactorioObject obj, float amount, UnitOfMeasure unit, out float newAmount, SchemeColor color = SchemeColor.None) {
            using var group = gui.EnterGroup(default, RectAllocator.Stretch, spacing: 0f);
            group.SetWidth(3f);
            newAmount = amount;
            var evt = GoodsWithAmountEvent.None;
            if (gui.BuildFactorioObjectButton(obj, 3f, MilestoneDisplay.Contained, color)) {
                evt = GoodsWithAmountEvent.ButtonClick;
            }

            if (gui.BuildTextInput(DataUtils.FormatAmount(amount, unit), out string newText, null, Icon.None, true, default, RectAlignment.Middle, SchemeColor.Secondary)) {
                if (DataUtils.TryParseAmount(newText, out newAmount, unit)) {
                    evt = GoodsWithAmountEvent.TextEditing;
                }
            }

            if (gui.action == ImGuiAction.MouseScroll && gui.ConsumeEvent(gui.lastRect)) {
                float digit = MathF.Pow(10, MathF.Floor(MathF.Log10(amount) - 2f));
                newAmount = MathF.Round((amount / digit) + gui.actionParameter) * digit;
                evt = GoodsWithAmountEvent.TextEditing;
            }

            return evt;
        }
    }
}
