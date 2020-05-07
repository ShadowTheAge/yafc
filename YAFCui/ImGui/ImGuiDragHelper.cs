using System.Numerics;
using SDL2;

namespace YAFC.UI
{
    public class ImGuiDragHelper : IPanel
    {
        private readonly ImGui dragContent = new ImGui(null, default);
        private ImGui dragTarget;
        private Rect dragRect;
        private uint button;
        private Vector2 offset;
        private Vector2 size;

        public bool dragging => dragTarget != null;
        public void BeginDraggingContent(ImGui target, Rect content, uint button = SDL.SDL_BUTTON_LEFT, SchemeColor backgroundColor = SchemeColor.PureBackground, RectangleBorder border = RectangleBorder.Thin)
        {
            target.ExportDrawCommandsTo(content, dragContent);
            dragContent.DrawRectangle(new Rect(default, dragContent.contentSize), backgroundColor, border);
            this.button = button;
            size = content.Size;
            if (target != dragTarget || content != dragRect)
            {
                offset = content.Position - target.mousePosition;
            }
            InputSystem.Instance.SetMouseDownPanel(this);
            dragRect = content;
            dragTarget = target;
            dragContent.parent?.Rebuild();
        }
        
        public void Build(ImGui gui)
        {
            if (dragTarget == null)
                return;
            if (!gui.isBuilding)
                return;
            if (InputSystem.Instance.mouseDownButton == button)
            {
                var rect = dragTarget.TranslateRect(dragRect, gui);
                rect.Position = gui.mousePosition + offset;
                gui.DrawPanel(rect, dragContent);
            }
        }

        public void TryDrag(ImGui gui, Rect handle, Rect draggable, SchemeColor backgroundColor = SchemeColor.PureBackground, RectangleBorder border = RectangleBorder.Thin)
        {
            if (gui.action == ImGuiAction.MouseDown && InputSystem.Instance.mouseDownButton == SDL.SDL_BUTTON_LEFT && gui.actionParameter == SDL.SDL_BUTTON_LEFT && gui.ConsumeMouseDown(handle))
                return;
            if (gui.action == ImGuiAction.Build && (dragTarget == gui && dragRect == draggable || gui.IsMouseDown(handle, SDL.SDL_BUTTON_LEFT)))
            {
                BeginDraggingContent(gui, draggable, SDL.SDL_BUTTON_LEFT, backgroundColor, border);
            }
        }

        public void MouseDown(int button) {}
        public void MouseUp(int button)
        {
            if (dragTarget != null)
            {
                dragTarget.Rebuild();
                dragTarget = null;
                dragContent.parent?.Rebuild();
            }
        }
        public void MouseMove(int mouseDownButton) {}
        public void MouseScroll(int delta) {}
        public void MarkEverythingForRebuild() {}
        public Vector2 CalculateState(float width, float pixelsPerUnit) => size;
        public void Present(Window window, Rect position, Rect screenClip, ImGui parent) {}
        public IPanel HitTest(Vector2 position) => this;
        public void MouseExit() {}
        public void MouseLost() {}
        public bool mouseCapture => false;
    }
}