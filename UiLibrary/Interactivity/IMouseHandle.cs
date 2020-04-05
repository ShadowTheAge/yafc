using SDL2;

namespace UI
{
    public interface IMouseHandle {}

    public interface IMouseClickHandle : IMouseHandle
    {
        void MouseClickUpdateState(bool mouseOverAndDown, int button);
        void MouseClick(int button);
    }

    public interface IMouseEnterHandle : IMouseHandle
    {
        void MouseEnter();
        void MouseExit();
    }

    public interface IMouseDragHandle : IMouseHandle
    {
        void BeginDrag();
        void Drag(IMouseDropHandle overTarget);
        void EndDrag(IMouseDropHandle dropTarget);
    }

    public interface IMouseDropHandle : IMouseHandle {}
}