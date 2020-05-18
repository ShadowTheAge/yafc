using System;
using System.Numerics;
using System.Threading.Tasks;
using Google.OrTools.ConstraintSolver;
using SDL2;
using YAFC.UI;

namespace YAFC
{
    // Pseudo screen is not an actual screen, it is a panel shown in the middle of the main screen
    public abstract class PseudoScreen : IKeyboardFocus
    {
        public readonly ImGui contents;
        private readonly float width;
        protected bool opened;

        protected PseudoScreen(float width = 40f)
        {
            this.width = width;
            contents = new ImGui(Build, ImGuiUtils.DefaultScreenPadding);
            contents.boxColor = SchemeColor.PureBackground;
        }

        public virtual void Open()
        {
            opened = true;
        }
        public abstract void Build(ImGui gui);

        public void Build(ImGui gui, Vector2 screenSize)
        {
            if (gui.isBuilding)
            {
                var contentSize = contents.CalculateState(width, gui.pixelsPerUnit);
                var position = (screenSize - contentSize) / 2;
                var rect = new Rect(position, contentSize);
                gui.DrawPanel(rect, contents);
                gui.DrawRectangle(rect, SchemeColor.None, RectangleBorder.Full);
            }
        }

        protected void BuildHeader(ImGui gui, string text, bool closeButton = true)
        {
            gui.BuildText(text, Font.header, false, RectAlignment.Middle);
            if (closeButton)
            {
                var closeButtonRect = new Rect(width-3f, 0f, 3f, 2f);
                if (gui.isBuilding)
                {
                    var isOver = gui.IsMouseOver(closeButtonRect);
                    var closeButtonCenter = Rect.Square(closeButtonRect.Center, 1f);
                    gui.DrawIcon(closeButtonCenter, Icon.Close, isOver ? SchemeColor.ErrorText : SchemeColor.BackgroundText);
                }
                if (gui.BuildButton(closeButtonRect, SchemeColor.None, SchemeColor.Error) == ImGuiUtils.Event.Click)
                    Close(false);
            }
        }

        protected virtual void Close(bool save = true)
        {
            if (save)
                Save();
            opened = false;
            InputSystem.Instance.SetDefaultKeyboardFocus(null);
            MainScreen.Instance.ClosePseudoScreen(this);
        }
        
        protected virtual void Save() {}

        public void Rebuild() => contents.Rebuild();

        public virtual void KeyDown(SDL.SDL_Keysym key)
        {
            if (key.scancode == SDL.SDL_Scancode.SDL_SCANCODE_ESCAPE)
                Close(false);
        }

        public virtual void TextInput(string input) {}

        public virtual void KeyUp(SDL.SDL_Keysym key) {}

        public virtual void FocusChanged(bool focused) {}
    }

    public abstract class PseudoScreen<T> : PseudoScreen
    {
        protected PseudoScreen(float width = 40f) : base(width) {}
        protected Action<bool, T> complete;

        protected void CloseWithResult(T result)
        {
            complete?.Invoke(true, result);
            complete = null;
            Close(true);
        }

        protected override void Close(bool save = true)
        {
            complete?.Invoke(false, default);
            complete = null;
            base.Close(save);
        }
    }
}