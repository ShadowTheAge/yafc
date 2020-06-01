using System;
using System.Numerics;
using YAFC.Model;
using YAFC.UI;

namespace YAFC
{
    public class MainScreenTabBar
    {
        private readonly MainScreen screen;
        private readonly ImGui tabs;
        public float maxScroll { get; private set; }
        
        public MainScreenTabBar(MainScreen screen)
        {
            this.screen = screen;
            tabs = new ImGui(BuildContents, default, RectAllocator.LeftRow, true);
        }

        private void BuildContents(ImGui gui)
        {
            gui.allocator = RectAllocator.LeftRow;
            gui.spacing = 0f;
            var changePage = false;
            ProjectPage changePageTo = null;
            ProjectPage prevPage = null;
            var right = 0f;
            var project = screen.project;
            for (var i = 0; i < project.displayPages.Count; i++)
            {
                var pageGuid = project.displayPages[i];
                var page = project.FindPage(pageGuid);
                if (page == null) continue;
                if (changePage && changePageTo == null)
                    changePageTo = page;
                using (gui.EnterGroup(new Padding(0.5f, 0.2f, 0.2f, 0.5f)))
                {
                    gui.spacing = 0.2f;
                    if (page.icon != null)
                        gui.BuildIcon(page.icon.icon);
                    else gui.AllocateRect(0f, 1.5f);
                    gui.BuildText(page.name);
                    if (gui.BuildButton(Icon.Close, size:0.8f))
                    {
                        changePageTo = prevPage;
                        changePage = true;
                        project.RecordUndo(true).displayPages.RemoveAt(i);
                        i--;
                    }
                }

                right = gui.lastRect.Right;

                if (gui.DoListReordering(gui.lastRect, gui.lastRect, i, out var from))
                    project.RecordUndo(true).displayPages.MoveListElementIndex(from, i);

                var isActive = screen.activePage == page;
                if (isActive && gui.isBuilding)
                    gui.DrawRectangle(new Rect(gui.lastRect.X, gui.lastRect.Bottom - 0.4f, gui.lastRect.Width, 0.4f), SchemeColor.Primary);
                var evt = gui.BuildButton(gui.lastRect, isActive ? SchemeColor.Background : SchemeColor.BackgroundAlt,
                    isActive ? SchemeColor.Background : SchemeColor.Grey);
                if (evt == ImGuiUtils.Event.Click)
                {
                    if (!isActive)
                    {
                        changePage = true;
                        changePageTo = page;
                    }
                    else ProjectPageSettingsPanel.Show(page);
                }

                prevPage = page;
                
            }

            gui.SetMinWidth(right);

            if (changePage)
                screen.SetActivePage(changePageTo);
        }

        public void Build(ImGui gui)
        {
            var rect = gui.RemainingRow().AllocateRect(0f, 2.1f, RectAlignment.Full);
            switch (gui.action)
            {
                case ImGuiAction.Build:
                    gui.DrawPanel(rect, tabs);
                    var measuredSize = tabs.CalculateState(0f, gui.pixelsPerUnit);
                    maxScroll = MathF.Max(0f, measuredSize.X - rect.Width);
                    break;
                case ImGuiAction.MouseScroll:
                    if (gui.ConsumeEvent(rect))
                    {
                        var clampedX = MathUtils.Clamp(-tabs.offset.X + 6f * gui.actionParameter, 0, maxScroll);
                        tabs.offset = new Vector2(-clampedX , 0f);
                    }
                    break;
            }
        }
    }
}