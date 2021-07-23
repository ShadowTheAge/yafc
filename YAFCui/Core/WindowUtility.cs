using System;
using SDL2;

namespace YAFC.UI
{
    // Utility window is not hardware-accelerated and auto-size (and not resizable)
    public abstract class WindowUtility : Window
    {
        private int windowWidth, windowHeight;
        private Window parent;
        
        public WindowUtility(Padding padding) : base(padding) {}
        
        protected void Create(string title, float width, Window parent)
        {
            if (visible)
                return;
            this.parent = parent;
            contentSize.X = width;
            var display = parent == null ? 0 : SDL.SDL_GetWindowDisplayIndex(parent.window);
            pixelsPerUnit = CalculateUnitsToPixels(display);
            contentSize = rootGui.CalculateState(width, pixelsPerUnit);
            windowWidth = rootGui.UnitsToPixels(contentSize.X);
            windowHeight = rootGui.UnitsToPixels(contentSize.Y);
            var flags = SDL.SDL_WindowFlags.SDL_WINDOW_MOUSE_FOCUS;
            if (parent != null)
                flags |= SDL.SDL_WindowFlags.SDL_WINDOW_SKIP_TASKBAR | SDL.SDL_WindowFlags.SDL_WINDOW_ALWAYS_ON_TOP;
            window = SDL.SDL_CreateWindow(title,
                SDL.SDL_WINDOWPOS_CENTERED_DISPLAY(display),
                SDL.SDL_WINDOWPOS_CENTERED_DISPLAY(display),
                windowWidth,
                windowHeight,
                flags
            );
            surface = new SoftwareDrawingSurface(this);
            base.Create();
        }

        private void CheckSizeChange()
        {
            var newWindowWidth = rootGui.UnitsToPixels(contentSize.X);
            var newWindowHeight = rootGui.UnitsToPixels(contentSize.Y);
            if (windowWidth != newWindowWidth || windowHeight != newWindowHeight)
            {
                windowWidth = newWindowWidth;
                windowHeight = newWindowHeight;
                SDL.SDL_SetWindowSize(window, newWindowWidth, newWindowHeight);
                WindowResize();
            }
        }

        protected override void MainRender()
        {
            CheckSizeChange();
            base.MainRender();
            if (surface.valid)
                SDL.SDL_UpdateWindowSurface(window);
        }

        protected internal override void Close()
        {
            base.Close();
            parent = null;
        }

        // TODO this is work-around for inability to create utility or modal window in SDL2
        // Fake utility windows are closed on focus lost
        public override void Minimized()
        {
            if (parent != null)
                Close();
        }
    }
}