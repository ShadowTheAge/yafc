using System;
using System.Collections.Generic;
using System.Numerics;
using SDL2;
using YAFC.Model;
using YAFC.UI;

namespace YAFC
{
    public static class ImmediateWidgets
    {
        public static void BuildFactorioObjectIcon(this ImGui gui, FactorioObject obj, bool contain = false, float size = 2f)
        {
            if (obj == null)
            {
                gui.BuildIcon(Icon.None, size);
                return;
            }
            var color = obj.IsAccessible() ? SchemeColor.Source : SchemeColor.SourceFaint;
            gui.BuildIcon(obj.icon, size, color);
            if (gui.action == ImGuiAction.Build)
            {
                var milestone = Milestones.GetHighest(obj);
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

        public static bool BuildFactorioObjectButton(this ImGui gui, Rect rect, FactorioObject obj)
        {
            var evt = gui.BuildButton(rect, SchemeColor.None, SchemeColor.Grey, button: SDL.SDL_BUTTON_MIDDLE);
            if (obj != null)
            {
                if (evt == ImGuiUtils.Event.MouseOver)
                    MainScreen.Instance.ShowTooltip(obj, gui, rect);
                else if (evt == ImGuiUtils.Event.Click)
                    DependencyExplorer.Show(obj);
            }
            return gui.BuildButtonClick(rect);
        }

        public static bool BuildFactorioObjectButton(this ImGui gui, FactorioObject obj, float size = 2f, bool contain = false)
        {
            gui.BuildFactorioObjectIcon(obj, contain, size);
            return gui.BuildFactorioObjectButton(gui.lastRect, obj);
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
                    gui.BuildFactorioObjectIcon(elem, false, 3f);
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
    }
}