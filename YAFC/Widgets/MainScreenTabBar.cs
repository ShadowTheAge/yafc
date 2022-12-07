using System;
using System.Numerics;
using SDL2;
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
            var changePage = 0;
            ProjectPage changePageTo = null;
            ProjectPage prevPage = null;
            var right = 0f;
            var project = screen.project;
            for (var i = 0; i < project.displayPages.Count; i++)
            {
                var pageGuid = project.displayPages[i];
                var page = project.FindPage(pageGuid);
                if (page == null) continue;
                if (changePage > 0 && changePageTo == null)
                    changePageTo = page;
                var isActive = screen.activePage == page;
                var isSecondary = screen.secondaryPage == page;
                using (gui.EnterGroup(new Padding(0.5f, 0.2f, 0.2f, 0.5f)))
                {
                    gui.spacing = 0.2f;
                    if (page.icon != null)
                        gui.BuildIcon(page.icon.icon);
                    else gui.AllocateRect(0f, 1.5f);
                    gui.BuildText(page.name);
                    if (gui.BuildButton(Icon.Close, size:0.8f))
                    {
                        if (isActive || isSecondary)
                        {
                            changePageTo = prevPage;
                            changePage = isActive ? 1 : 2;
                        }
                        project.RecordUndo(true).displayPages.RemoveAt(i);
                        i--;
                    }
                }

                right = gui.lastRect.Right;

                if (gui.DoListReordering(gui.lastRect, gui.lastRect, i, out var from))
                    project.RecordUndo(true).displayPages.MoveListElementIndex(from, i);

                if ((isActive || isSecondary) && gui.isBuilding)
                    gui.DrawRectangle(new Rect(gui.lastRect.X, gui.lastRect.Bottom - 0.4f, gui.lastRect.Width, 0.4f), isActive ? SchemeColor.Primary : SchemeColor.Secondary);
                var evt = gui.BuildButton(gui.lastRect, isActive ? SchemeColor.Background : SchemeColor.BackgroundAlt, (isActive || isSecondary) ? SchemeColor.Background : SchemeColor.Grey);
                if (evt == ButtonEvent.Click)
                {
                    changePage = InputSystem.Instance.control ? 2 : 1;
                    changePageTo = page;
                } 
                else if (evt == ButtonEvent.MouseOver)
                    MainScreen.Instance.ShowTooltip(gui, page, false, gui.lastRect);

                prevPage = page;
                
            }

            gui.SetMinWidth(right);

            if (changePage > 0)
            {
                if (changePage == 1)
                {
                    if (changePageTo == screen.activePage)
                        ProjectPageSettingsPanel.Show(changePageTo);
                    else screen.SetActivePage(changePageTo);
                }
                else
                {
                    screen.SetSecondaryPage(changePageTo == screen.secondaryPage ? null : changePageTo);
                }
            }
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