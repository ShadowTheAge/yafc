using System;
using SDL2;

namespace Yafc.UI {
    // Utility window is not hardware-accelerated and auto-size (and not resizable)
    public abstract class WindowUtility(Padding padding) : Window(padding) {
        private int windowWidth, windowHeight;
        private Window parent;

        protected void Create(string title, float width, Window parent) {
            if (visible) {
                return;
            }

            this.parent = parent;
            contentSize.X = width;
            int display = parent == null ? 0 : SDL.SDL_GetWindowDisplayIndex(parent.window);
            pixelsPerUnit = CalculateUnitsToPixels(display);
            contentSize = rootGui.CalculateState(width, pixelsPerUnit);
            windowWidth = rootGui.UnitsToPixels(contentSize.X);
            windowHeight = rootGui.UnitsToPixels(contentSize.Y);
            var flags = SDL.SDL_WindowFlags.SDL_WINDOW_MOUSE_FOCUS;
            if (parent != null) {
                flags |= SDL.SDL_WindowFlags.SDL_WINDOW_SKIP_TASKBAR | SDL.SDL_WindowFlags.SDL_WINDOW_ALWAYS_ON_TOP;
            }

            window = SDL.SDL_CreateWindow(title,
                SDL.SDL_WINDOWPOS_CENTERED_DISPLAY(display),
                SDL.SDL_WINDOWPOS_CENTERED_DISPLAY(display),
                windowWidth,
                windowHeight,
                flags
            );
            surface = new UtilityWindowDrawingSurface(this);
            base.Create();
        }

        internal override void WindowResize() {
            (surface as UtilityWindowDrawingSurface).OnResize();
            base.WindowResize();
        }

        private void CheckSizeChange() {
            int newWindowWidth = rootGui.UnitsToPixels(contentSize.X);
            int newWindowHeight = rootGui.UnitsToPixels(contentSize.Y);
            if (windowWidth != newWindowWidth || windowHeight != newWindowHeight) {
                windowWidth = newWindowWidth;
                windowHeight = newWindowHeight;
                SDL.SDL_SetWindowSize(window, newWindowWidth, newWindowHeight);
                WindowResize();
            }
        }

        protected override void MainRender() {
            CheckSizeChange();
            base.MainRender();
        }

        protected internal override void Close() {
            base.Close();
            parent = null;
        }

        // TODO this is work-around for inability to create utility or modal window in SDL2
        // Fake utility windows are closed on focus lost
        public override void Minimized() {
            if (parent != null) {
                Close();
            }
        }
    }

    internal class UtilityWindowDrawingSurface : SoftwareDrawingSurface {
        public override Window window { get; }

        public UtilityWindowDrawingSurface(WindowUtility window) : base(IntPtr.Zero, window.pixelsPerUnit) {
            this.window = window;
            InvalidateRenderer();
        }

        private void InvalidateRenderer() {
            surface = SDL.SDL_GetWindowSurface(window.window);
            renderer = SDL.SDL_CreateSoftwareRenderer(surface);
            _ = SDL.SDL_SetRenderDrawBlendMode(renderer, SDL.SDL_BlendMode.SDL_BLENDMODE_BLEND);
        }

        public void OnResize() {
            InvalidateRenderer();
        }

        public override void Dispose() {
            base.Dispose();
            surface = IntPtr.Zero;
        }

        public override void Present() {
            base.Present();
            if (surface != IntPtr.Zero) {
                _ = SDL.SDL_UpdateWindowSurface(window.window);
            }
        }
    }
}
