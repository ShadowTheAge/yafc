using System;
using System.Numerics;
using System.Threading.Tasks;
using SDL2;
using YAFC.UI;

namespace YAFC
{
    // Pseudo screen is not an actual screen, it is a panel shown in the middle of the main screen
    public abstract class PseudoScreen : IGui, IKeyboardFocus
    {
        public readonly ImGui contents;
        private readonly float width;

        protected PseudoScreen(float width)
        {
            this.width = width;
            contents = new ImGui(this, ImGuiUtils.DefaultScreenPadding);
            contents.boxColor = SchemeColor.PureBackground;
        }

        public abstract void Build(ImGui gui);

        public void Build(ImGui gui, Vector2 screenSize)
        {
            if (gui.action == ImGuiAction.Build)
            {
                var contentSize = contents.CalculateState(width, gui.pixelsPerUnit);
                var position = (screenSize - contentSize) / 2;
                var rect = new Rect(position, contentSize);
                gui.DrawPanel(rect, contents);
                gui.DrawRectangle(rect, SchemeColor.None, RectangleBorder.Full);
            }
        }

        protected virtual void Close()
        {
            InputSystem.Instance.SetDefaultKeyboardFocus(null);
            MainScreen.Instance.ClosePseudoScreen(this);
        }

        public void Rebuild() => contents.Rebuild();

        public virtual void KeyDown(SDL.SDL_Keysym key)
        {
            if (key.scancode == SDL.SDL_Scancode.SDL_SCANCODE_ESCAPE)
                Close();
        }

        public virtual void TextInput(string input) {}

        public virtual void KeyUp(SDL.SDL_Keysym key) {}

        public virtual void FocusChanged(bool focused) {}
    }

    public abstract class PseudoScreen<T> : PseudoScreen
    {
        protected PseudoScreen(float width) : base(width) {}
        protected Action<T> complete;

        protected void CloseWithResult(T result)
        {
            complete?.Invoke(result);
            complete = null;
            Close();
        }

        protected override void Close()
        {
            complete = null;
            base.Close();
        }
    }
}