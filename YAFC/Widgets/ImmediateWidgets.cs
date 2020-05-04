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
        ButtonClick,
        TextEditing,
    }
    
    public static class ImmediateWidgets
    {
        public static void BuildFactorioObjectIcon(this ImGui gui, FactorioObject obj, MilestoneDisplay display = MilestoneDisplay.Normal, float size = 2f)
        {
            if (obj == null)
            {
                gui.BuildIcon(Icon.None, size);
                return;
            }
            
            var color = obj.IsAccessible() ? SchemeColor.Source : SchemeColor.SourceFaint;
            gui.BuildIcon(obj.icon, size, color);
            if (gui.action == ImGuiAction.Build && display != MilestoneDisplay.None)
            {
                var contain = (display & MilestoneDisplay.Contained) != 0;
                var milestone = Milestones.GetHighest(obj, display >= MilestoneDisplay.All);
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

        public static bool BuildFactorioObjectButton(this ImGui gui, Rect rect, FactorioObject obj, SchemeColor bgColor = SchemeColor.None)
        {
            var overColor = bgColor == SchemeColor.None ? SchemeColor.Grey : bgColor + 1;
            var evt = gui.BuildButton(rect, bgColor, overColor, button: SDL.SDL_BUTTON_MIDDLE);
            if (obj != null)
            {
                if (evt == ImGuiUtils.Event.MouseOver)
                    MainScreen.Instance.ShowTooltip(obj, gui, rect);
                else if (evt == ImGuiUtils.Event.Click)
                    DependencyExplorer.Show(obj);
            }
            return gui.BuildButtonClick(rect);
        }

        public static bool BuildFactorioObjectButton(this ImGui gui, FactorioObject obj, float size = 2f, MilestoneDisplay display = MilestoneDisplay.Normal, SchemeColor bgColor = SchemeColor.None)
        {
            gui.BuildFactorioObjectIcon(obj, display, size);
            return gui.BuildFactorioObjectButton(gui.lastRect, obj, bgColor);
        }
        
        public static bool BuildInlineObjectList<T>(this ImGui gui, IEnumerable<T> list, IComparer<T> ordering, string header, out T selected, int maxCount = 10) where T:FactorioObject
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
                using (gui.EnterRow())
                {
                    gui.BuildFactorioObjectIcon(elem, MilestoneDisplay.Contained, 3f);
                    gui.BuildText(elem.locName, wrap:true);
                }

                if (gui.BuildFactorioObjectButton(gui.lastRect, elem))
                    selected = elem;
            }

            return selected != null;
        }
        
        public static bool BuildInlineObejctListAndButton<T>(this ImGui gui, IReadOnlyList<T> list, IComparer<T> ordering, Action<T> select, string header) where T:FactorioObject
        {
            var close = false;
            if (gui.BuildInlineObjectList(list, ordering, header, out var selected, 3))
            {
                select(selected);
                close = true;
            }
            if (list.Count > 3 && gui.BuildButton("See full list"))
            {
                SelectObjectPanel.Select(list, header, select, ordering);
                close = true;
            }
            return close;
        }

        public static bool BuildGoodsWithAmount(this ImGui gui, IGoodsWithAmount goodsWithAmount, SchemeColor color = SchemeColor.None)
        {
            gui.allocator = RectAllocator.Stretch;
            gui.spacing = 0f;
            var clicked = gui.BuildFactorioObjectButton(goodsWithAmount.goods, 3f, MilestoneDisplay.Contained, color);
            gui.BuildText(DataUtils.FormatAmount(goodsWithAmount.amount, goodsWithAmount.goods.isPower), Font.text, false, RectAlignment.Middle);
            return clicked;
        }
        
        public static GoodsWithAmountEvent BuildGoodsWithEditableAmount(this ImGui gui, IGoodsWithAmount goodsWithAmount, out float newAmount, SchemeColor color = SchemeColor.None)
        {
            gui.allocator = RectAllocator.Stretch;
            gui.spacing = 0f;
            newAmount = goodsWithAmount.amount;
            var evt = GoodsWithAmountEvent.None;
            if (gui.BuildFactorioObjectButton(goodsWithAmount.goods, 3f, MilestoneDisplay.Contained, color))
                evt = GoodsWithAmountEvent.ButtonClick;
            if (gui.BuildTextInput(DataUtils.FormatAmount(goodsWithAmount.amount, goodsWithAmount.goods.isPower), out var newText, null, false, Icon.None, default, RectAlignment.Middle, SchemeColor.Secondary))
            {
                if (DataUtils.TryParseAmount(newText, out newAmount, goodsWithAmount.goods.isPower))
                    evt = GoodsWithAmountEvent.TextEditing;
            }

            return evt;
        }
    }
}