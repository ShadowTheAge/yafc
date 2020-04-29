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

        public virtual void KeyDown(SDL.SDL_Keysym key) {}

        public virtual void TextInput(string input) {}

        public virtual void KeyUp(SDL.SDL_Keysym key) {}

        public virtual void FocusChanged(bool focused) {}
    }

    public abstract class PseudoScreen<T> : PseudoScreen
    {
        protected PseudoScreen(float width) : base(width) {}
        protected TaskCompletionSource<T> taskSource;

        protected void CloseWithResult(T result)
        {
            taskSource?.TrySetResult(result);
            taskSource = null;
            Close();
        }

        protected override void Close()
        {
            taskSource?.TrySetResult(default);
            taskSource = null;
            base.Close();
        }
    }
}