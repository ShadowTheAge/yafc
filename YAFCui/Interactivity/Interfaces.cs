using System.Drawing;
using SDL2;

namespace UI
{
    public interface IMouseHandle {}

    public interface IMouseClickHandle : IMouseHandle
    {
        void MouseClickUpdateState(bool mouseOverAndDown, int button, RenderBatch batch);
        void MouseClick(int button, RenderBatch batch);
    }

    public interface IMouseScrollHandle : IMouseHandle
    {
        void Scroll(int delta, RenderBatch batch);
    }

    public interface IMouseEnterHandle : IMouseHandle
    {
        void MouseEnter(RenderBatch batch);
        void MouseExit(RenderBatch batch);
    }

    public interface IMouseDragHandle : IMouseHandle
    {
        void MouseDown(PointF position, RenderBatch batch);
        void BeginDrag(PointF position, RenderBatch batch);
        void Drag(PointF position, RenderBatch batch);
        void EndDrag(PointF position, RenderBatch batch);
    }

    public interface IKeyboardFocus
    {
        void KeyDown(SDL.SDL_Keysym key);
        void TextInput(string input);
        void KeyUp(SDL.SDL_Keysym key);
        void FocusChanged(bool focused);
        void UpdateSelected();
    }
}