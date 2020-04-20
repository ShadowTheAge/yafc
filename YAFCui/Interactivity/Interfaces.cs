using System.Numerics;
using SDL2;

namespace YAFC.UI
{
    public interface IMouseHandle {}

    public interface IMouseClickHandle : IMouseHandle
    {
        void MouseClickUpdateState(bool mouseOverAndDown, int button, UiBatch batch);
        void MouseClick(int button, UiBatch batch);
    }

    public interface IMouseScrollHandle : IMouseHandle
    {
        void Scroll(int delta, UiBatch batch);
    }

    public interface IMouseEnterHandle : IMouseHandle
    {
        void MouseEnter(RaycastResult<IMouseEnterHandle> raycast);
        void MouseExit(UiBatch batch);
    }

    public interface IMouseDragHandle : IMouseHandle
    {
        void MouseDown(Vector2 position, UiBatch batch);
        void BeginDrag(Vector2 position, UiBatch batch);
        void Drag(Vector2 position, UiBatch batch);
        void EndDrag(Vector2 position, UiBatch batch);
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