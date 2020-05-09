using System;
using System.Numerics;
using YAFC.Model;
using YAFC.UI;

namespace YAFC
{
    public abstract class ProjectPageView : Scrollable, IGui
    {
        protected ProjectPageView() : base(true, true, false)
        {
            headerContent = new ImGui(this, default, RectAllocator.LeftAlign);
            bodyContent = new ImGui(this, default, RectAllocator.LeftAlign, true);
        }

        protected readonly ImGui headerContent;
        protected readonly ImGui bodyContent;
        private float contentWidth, headerHeight, contentHeight;
        public abstract void BuildHeader(ImGui gui);
        public abstract void BuildContent(ImGui gui);

        public virtual void Rebuild(bool visualOnly = false)
        {
            headerContent.Rebuild(); 
            bodyContent.Rebuild();
        }

        public abstract void SetModel(ProjectPage page);

        public void Build(ImGui gui, Vector2 visibleSize)
        {
            if (gui.isBuilding)
            {
                gui.spacing = 0f;
                var position = gui.AllocateRect(0f, 0f, 0f).Position;
                var headerSize = headerContent.CalculateState(visibleSize.X, gui.pixelsPerUnit);
                contentWidth = headerSize.X;
                headerHeight = headerSize.Y;
                var headerRect = gui.AllocateRect(visibleSize.X, headerHeight);
                position.Y += headerHeight;
                var contentSize = bodyContent.CalculateState(visibleSize.X, gui.pixelsPerUnit);
                if (contentSize.X > contentWidth)
                    contentWidth = contentSize.X;
                contentHeight = contentSize.Y;
                gui.DrawPanel(headerRect, headerContent);
            }
            else
                gui.AllocateRect(contentWidth, headerHeight);
            
            base.Build(gui, visibleSize.Y - headerHeight);
        }

        protected override Vector2 MeasureContent(Rect rect, ImGui gui)
        {
            return new Vector2(contentWidth, contentHeight);
        }

        protected override void PositionContent(ImGui gui, Rect viewport)
        {
            headerContent.offset = new Vector2(-scrollX, 0);
            bodyContent.offset = -scroll2d;
            gui.DrawPanel(viewport, bodyContent);
        }

        public void Build(ImGui gui)
        {
            if (gui == headerContent)
                BuildHeader(gui);
            else if (gui == bodyContent)
                BuildContent(gui);
        }

        public abstract void CreateModelDropdown(ImGui gui1, Type type, Project project, ref bool close);
    }
}