using System.Drawing;
using SDL2;

namespace UI
{
    public interface IMouseHandle {}

    public interface IMouseClickHandle : IMouseHandle
    {
        void MouseClickUpdateState(bool mouseOverAndDown, int button);
        void MouseClick(int button);
    }

    public interface IMouseScrollHandle : IMouseHandle
    {
        void Scroll(int delta);
    }

    public interface IMouseEnterHandle : IMouseHandle
    {
        void MouseEnter();
        void MouseExit();
    }

    public interface IMouseDragHandle : IMouseHandle
    {
        void MouseDown(PointF position);
        void BeginDrag(PointF position);
        void Drag(PointF position, IMouseDropHandle overTarget);
        void EndDrag(PointF position, IMouseDropHandle dropTarget);
    }

    public interface IMouseDropHandle : IMouseHandle {}

    public interface IKeyboardFocus
    {
        void KeyDown(SDL.SDL_Keysym key);
        void TextInput(string input);
        void KeyUp(SDL.SDL_Keysym key);
        void FocusChanged(bool focused);
        void UpdateSelected();
    }
}