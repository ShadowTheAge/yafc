using System.Numerics;
using SDL2;

namespace YAFC.UI
{
    public interface IMouseHandleBase {}

    public interface IMouseHandle : IMouseHandleBase
    {
        void MouseEnter(HitTestResult<IMouseHandle> hitTest);
        void MouseExit(UiBatch batch);
        void MouseClick(int button, UiBatch batch);
    }

    public interface IMouseMoveHandle : IMouseHandle
    {
        void MouseMove(Vector2 position, UiBatch batch);
    }

    public interface IMouseScrollHandle : IMouseHandleBase
    {
        void Scroll(int delta, UiBatch batch);
    }

    public interface IMouseDragHandle : IMouseHandle
    {
        void BeginDrag(Vector2 position, int button, UiBatch batch);
        void Drag(Vector2 position, int button, UiBatch batch);
        void EndDrag(Vector2 position, int button, UiBatch batch);
    }

    public interface IKeyboardFocus
    {
        void KeyDown(SDL.SDL_Keysym key);
        void TextInput(string input);
        void KeyUp(SDL.SDL_Keysym key);
        void FocusChanged(bool focused);
    }

    public interface IMouseFocus : IMouseHandleBase
    {
        void FocusChanged(bool focused);
    }
}