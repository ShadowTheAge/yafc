using System.Drawing;
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
    }
}