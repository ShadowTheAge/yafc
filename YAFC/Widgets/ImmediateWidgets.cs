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

        public static bool BuildFactorioObjectButton(this ImGui gui, Rect rect, FactorioObject obj, SchemeColor bgColor = SchemeColor.None, bool extendHeader = false)
        {
            var overColor = bgColor == SchemeColor.None ? SchemeColor.Grey : bgColor + 1;
            var evt = gui.BuildButton(rect, bgColor, overColor, button: 0);
            if (obj != null)
            {
                if (evt == ImGuiUtils.Event.MouseOver)
                    MainScreen.Instance.ShowTooltip(obj, gui, rect, extendHeader);
                else if (evt == ImGuiUtils.Event.Click)
                {
                    if (gui.actionParameter == SDL.SDL_BUTTON_MIDDLE)
                    {
                        if (obj is Goods goods && obj.IsAccessible())
                            NeverEnoughItemsPanel.Show(goods, null);
                        else DependencyExplorer.Show(obj); 
                    }
                    else if (gui.actionParameter == SDL.SDL_BUTTON_LEFT)
                        return true;
                }
            }

            return false;
        }

        public static bool BuildFactorioObjectButton(this ImGui gui, FactorioObject obj, float size = 2f, MilestoneDisplay display = MilestoneDisplay.Normal, SchemeColor bgColor = SchemeColor.None, bool extendHeader = false)
        {
            gui.BuildFactorioObjectIcon(obj, display, size);
            return gui.BuildFactorioObjectButton(gui.lastRect, obj, bgColor, extendHeader);
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
                    gui.BuildFactorioObjectIcon(elem, MilestoneDisplay.Contained, 2f);
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

        public static bool BuildFactorioObjectWithAmount(this ImGui gui, FactorioObject goods, float amount, SchemeColor color = SchemeColor.None, bool isPower = false)
        {
            using (gui.EnterFixedPositioning(3f, 3f, default))
            {
                gui.allocator = RectAllocator.Stretch;
                gui.spacing = 0f;
                var clicked = gui.BuildFactorioObjectButton(goods, 3f, MilestoneDisplay.Contained, color);
                gui.BuildText(DataUtils.FormatAmount(amount, isPower), Font.text, false, RectAlignment.Middle);
                return clicked;
            }
        }
        
        public static GoodsWithAmountEvent BuildFactorioGoodsWithEditableAmount(this ImGui gui, Goods goods, float amount, out float newAmount, SchemeColor color = SchemeColor.None)
        {
            gui.allocator = RectAllocator.Stretch;
            gui.spacing = 0f;
            newAmount = amount;
            var evt = GoodsWithAmountEvent.None;
            if (gui.BuildFactorioObjectButton(goods, 3f, MilestoneDisplay.Contained, color))
                evt = GoodsWithAmountEvent.ButtonClick;
            if (gui.BuildTextInput(DataUtils.FormatAmount(amount, goods.isPower), out var newText, null, false, Icon.None, default, RectAlignment.Middle, SchemeColor.Secondary))
            {
                if (DataUtils.TryParseAmount(newText, out newAmount, goods.isPower))
                    evt = GoodsWithAmountEvent.TextEditing;
            }

            return evt;
        }
    }
}