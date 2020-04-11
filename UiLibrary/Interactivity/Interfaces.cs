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
        void BeginDrag();
        bool Drag(IMouseDropHandle overTarget);
        void EndDrag(IMouseDropHandle dropTarget);
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