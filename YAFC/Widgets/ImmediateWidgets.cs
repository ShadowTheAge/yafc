using System;
using System.Collections.Generic;
using System.Numerics;
using SDL2;
using YAFC.Model;
using YAFC.UI;

namespace YAFC
{
    public enum MilestoneDisplay
    {
        Normal,
        Contained,
        All,
        AllContained,
        None
    }
    
    public enum GoodsWithAmountEvent
    {
        None,
        LeftButtonClick,
        RightButtonClick,
        TextEditing,
    }
    
    public static class ImmediateWidgets
    {
        public static void BuildFactorioObjectIcon(this ImGui gui, FactorioObject obj, MilestoneDisplay display = MilestoneDisplay.Normal, float size = 2f)
        {
            if (obj == null)
            {
                gui.BuildIcon(Icon.Empty, size, SchemeColor.BackgroundTextFaint);
                return;
            }
            
            var color = obj.IsAccessible() ? SchemeColor.Source : SchemeColor.SourceFaint;
            gui.BuildIcon(obj.icon, size, color);
            if (gui.isBuilding && display != MilestoneDisplay.None)
            {
                var contain = (display & MilestoneDisplay.Contained) != 0;
                var milestone = Milestones.Instance.GetHighest(obj, display >= MilestoneDisplay.All);
                if (milestone != null)
                {
                    var psize = new Vector2(size/2f);
                    var delta = contain ? psize : psize / 2f;
                    var milestoneIcon = new Rect(gui.lastRect.BottomRight - delta, psize);
                    var icon = milestone == Database.voidEnergy ? DataUtils.HandIcon : milestone.icon;
                    gui.DrawIcon(milestoneIcon, icon, color);
                }
            }
        }
        
        public static bool BuildFloatInput(this ImGui gui, float value, out float newValue, UnitOfMeasure unit, Padding padding)
        {
            if (gui.BuildTextInput(DataUtils.FormatAmount(value, unit), out var newText, null, Icon.None, true, padding) && DataUtils.TryParseAmount(newText, out newValue, unit))
                return true;
            newValue = value;
            return false;
        }

        public static Click BuildFactorioObjectButton(this ImGui gui, Rect rect, FactorioObject obj, SchemeColor bgColor = SchemeColor.None, bool extendHeader = false)
        {
            SchemeColor overColor;
            if (bgColor == SchemeColor.None)
                overColor = SchemeColor.Grey;
            else
            {
                overColor = bgColor + 1;
                if (MainScreen.Instance.IsSameObjectHovered(gui, obj))
                    bgColor = overColor;
            }
            var evt = gui.BuildButton(rect, bgColor, overColor, button: 0);
            if (evt == ButtonEvent.MouseOver && obj != null)
                MainScreen.Instance.ShowTooltip(obj, gui, rect, extendHeader);
            else if (evt == ButtonEvent.Click)
            {
                if (gui.actionParameter == SDL.SDL_BUTTON_MIDDLE && obj != null)
                {
                    if (obj is Goods goods && obj.IsAccessible())
                        NeverEnoughItemsPanel.Show(goods);
                    else DependencyExplorer.Show(obj); 
                }
                else if (gui.actionParameter == SDL.SDL_BUTTON_LEFT)
                    return Click.Left;
                else if (gui.actionParameter == SDL.SDL_BUTTON_RIGHT)
                    return Click.Right;
            }

            return Click.None;
        }

        public static Click BuildFactorioObjectButton(this ImGui gui, FactorioObject obj, float size = 2f, MilestoneDisplay display = MilestoneDisplay.Normal, SchemeColor bgColor = SchemeColor.None, bool extendHeader = false)
        {
            gui.BuildFactorioObjectIcon(obj, display, size);
            return gui.BuildFactorioObjectButton(gui.lastRect, obj, bgColor, extendHeader);
        }

        public static Click BuildFactorioObjectButtonWithText(this ImGui gui, FactorioObject obj, string extraText = null, float size = 2f, MilestoneDisplay display = MilestoneDisplay.Normal)
        {
            using (gui.EnterRow())
            {
                gui.BuildFactorioObjectIcon(obj, display, size);
                var color = gui.textColor;
                if (obj != null && !obj.IsAccessible())
                    color += 1;
                if (Project.current.preferences.favourites.Contains(obj))
                    gui.BuildIcon(Icon.StarFull, 1f);
                if (extraText != null)
                {
                    gui.AllocateSpacing();
                    gui.allocator = RectAllocator.RightRow;
                    gui.BuildText(extraText, color:color);
                }
                gui.RemainingRow();
                gui.BuildText(obj == null ? "None" : obj.locName, wrap:true, color:color);
            }

            return gui.BuildFactorioObjectButton(gui.lastRect, obj);
        }
        
        public static bool BuildInlineObjectList<T>(this ImGui gui, IEnumerable<T> list, IComparer<T> ordering, string header, out T selected, int maxCount = 10,
            Predicate<T> checkmark = null, Func<T, string> extra = null) where T:FactorioObject
        {
            gui.BuildText(header, Font.subheader);
            var sortedList = new List<T>(list);
            sortedList.Sort(ordering ?? DataUtils.DefaultOrdering);
            selected = null;
            var count = 0;
            foreach (var elem in sortedList)
            {
                if (count++ >= maxCount)
                    break;
                var extraText = extra?.Invoke(elem);
                if (gui.BuildFactorioObjectButtonWithText(elem, extraText) == Click.Left)
                    selected = elem;
                if (checkmark != null && gui.isBuilding && checkmark(elem))
                    gui.DrawIcon(Rect.Square(new Vector2(gui.lastRect.Right-1f, gui.lastRect.Center.Y), 1.5f), Icon.Check, SchemeColor.Green);
            }

            return selected != null;
        }
        
        public static void BuildInlineObejctListAndButton<T>(this ImGui gui, ICollection<T> list, IComparer<T> ordering, Action<T> select, string header, int count = 6, bool multiple = false, Predicate<T> checkmark = null, bool allowNone = false, Func<T, string> extra = null) where T:FactorioObject
        {
            using (gui.EnterGroup(default, RectAllocator.Stretch))
            {
                if (gui.BuildInlineObjectList(list, ordering, header, out var selected, count, checkmark, extra))
                {
                    select(selected);
                    if (!multiple || !InputSystem.Instance.control)
                        gui.CloseDropdown();
                }
                if (allowNone && gui.BuildRedButton("Clear") && gui.CloseDropdown())
                    select(null);
                if (list.Count > count && gui.BuildButton("See full list") && gui.CloseDropdown())
                {
                    if(multiple)
                        SelectMultiObjectPanel.Select(list, header, select, ordering, allowNone, checkmark);
                    else
                        SelectSingleObjectPanel.Select(list, header, select, ordering, allowNone);
                }
                if (multiple && list.Count > 1)
                    gui.BuildText("Hint: ctrl+click to add multiple", wrap:true, color:SchemeColor.BackgroundTextFaint);
            }
        }

        public static Click BuildFactorioObjectWithAmount(this ImGui gui, FactorioObject goods, float amount, UnitOfMeasure unit, SchemeColor color = SchemeColor.None)
        {
            using (gui.EnterFixedPositioning(3f, 3f, default))
            {
                gui.allocator = RectAllocator.Stretch;
                gui.spacing = 0f;
                var clicked = gui.BuildFactorioObjectButton(goods, 3f, MilestoneDisplay.Contained, color);
                if (goods != null)
                {
                    gui.BuildText(DataUtils.FormatAmount(amount, unit), Font.text, false, RectAlignment.Middle);
                    if (InputSystem.Instance.control && gui.BuildButton(gui.lastRect, SchemeColor.None, SchemeColor.Grey) == ButtonEvent.MouseOver)
                        ShowPrecisionValueTootlip(gui, amount, unit, goods);
                }
                return clicked;
            }
        }

        public static void ShowPrecisionValueTootlip(ImGui gui, float amount, UnitOfMeasure unit, FactorioObject goods)
        {
            string text;
            switch (unit)
            {
                case UnitOfMeasure.PerSecond: case UnitOfMeasure.FluidPerSecond: case UnitOfMeasure.ItemPerSecond:
                    var perSecond = DataUtils.FormatAmountRaw(amount, 1f, "/s", formatSpec:DataUtils.PreciseFormat);
                    var perMinute = DataUtils.FormatAmountRaw(amount, 60f, "/m", formatSpec:DataUtils.PreciseFormat);
                    var perHour = DataUtils.FormatAmountRaw(amount, 3600f, "/h", formatSpec:DataUtils.PreciseFormat);
                    text = perSecond + "\n" + perMinute + "\n" + perHour;
                    if (goods is Item item)
                        text += DataUtils.FormatAmount(MathF.Abs(item.stackSize / amount), UnitOfMeasure.Second, "\n", " per stack");
                    break;
                default:
                    text = DataUtils.FormatAmount(amount, unit, precise: true);
                    break;
            }
            gui.ShowTooltip(gui.lastRect, x =>
            {
                x.BuildFactorioObjectButtonWithText(goods);
                x.BuildText(text, wrap:true);
            }, 10f);
        }

        public static void BuildObjectSelectDropDown<T>(this ImGui gui, ICollection<T> list, IComparer<T> ordering, Action<T> select, string header, int count = 6, bool multiple = false, Predicate<T> checkmark = null, bool allowNone = false, Func<T, string> extra = null) where T:FactorioObject
        {
            gui.ShowDropDown(imGui => imGui.BuildInlineObejctListAndButton(list, ordering, select, header, count, multiple, checkmark, allowNone, extra));
        }
        
        public static GoodsWithAmountEvent BuildFactorioObjectWithEditableAmount(this ImGui gui, FactorioObject obj, float amount, UnitOfMeasure unit, out float newAmount, SchemeColor color = SchemeColor.None, bool allowScroll = true)
        {
            using (var group = gui.EnterGroup(default, RectAllocator.Stretch, spacing:0f))
            {
                group.SetWidth(3f);
                newAmount = amount;
                var evt = gui.BuildFactorioObjectButton(obj, 3f, MilestoneDisplay.Contained, color) switch
                {
                    Click.Left => GoodsWithAmountEvent.LeftButtonClick,
                    Click.Right => GoodsWithAmountEvent.RightButtonClick,
                    _ => GoodsWithAmountEvent.None,
                };
                if (gui.BuildTextInput(DataUtils.FormatAmount(amount, unit), out var newText, null, Icon.None, true, default, RectAlignment.Middle, SchemeColor.Secondary))
                {
                    if (DataUtils.TryParseAmount(newText, out newAmount, unit))
                        evt = GoodsWithAmountEvent.TextEditing;
                }

                if (allowScroll && gui.action == ImGuiAction.MouseScroll && gui.ConsumeEvent(gui.lastRect))
                {
                    var digit = MathF.Pow(10, MathF.Floor(MathF.Log10(amount) - 2f));
                    newAmount = MathF.Round(amount / digit + gui.actionParameter) * digit;
                    evt = GoodsWithAmountEvent.TextEditing;
                }
                
                return evt;
            }
        }
    }
}