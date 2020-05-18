using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using SDL2;

namespace YAFC.UI
{
    public interface IKeyboardFocus
    {
        void KeyDown(SDL.SDL_Keysym key);
        void TextInput(string input);
        void KeyUp(SDL.SDL_Keysym key);
        void FocusChanged(bool focused);
    }
    
    public interface IMouseFocus
    {
        bool FilterPanel(IPanel panel);
        void FocusChanged(bool focused);
    }
    
    public sealed class InputSystem
    {
        public static readonly InputSystem Instance = new InputSystem();
        
        private InputSystem() {}

        public Window mouseOverWindow { get; private set; }
        private IPanel hoveringPanel;
        private IPanel mouseDownPanel;
        private IMouseFocus activeMouseFocus;
        private IKeyboardFocus activeKeyboardFocus;
        private IKeyboardFocus defaultKeyboardFocus;
        private SDL.SDL_Keymod keyMod;
        public int mouseDownButton { get; private set; } = -1;

        private IKeyboardFocus currentKeyboardFocus => activeKeyboardFocus ?? defaultKeyboardFocus;
        private readonly List<(SendOrPostCallback, object)> mouseUpCallbacks = new List<(SendOrPostCallback, object)>();

        public Vector2 mouseDownPosition { get; private set; }
        public Vector2 mousePosition { get; private set; }
        public Vector2 mouseDelta { get; private set; }

        public event Action<Window, IPanel, Vector2> GlobalMouseDown;
        public void DispatchOnGestureFinish(SendOrPostCallback callback, object state)
        {
            if (mouseDownButton == -1)
                Ui.DispatchInMainThread(callback, state);
            else mouseUpCallbacks.Add((callback, state));
        }

        public void SetKeyboardFocus(IKeyboardFocus focus)
        {
            if (focus == activeKeyboardFocus)
                return;
            currentKeyboardFocus?.FocusChanged(false);
            activeKeyboardFocus = focus;
            currentKeyboardFocus?.FocusChanged(true);
        }

        public void SetMouseFocus(IMouseFocus mouseFocus)
        {
            if (mouseFocus == activeMouseFocus)
                return;
            activeMouseFocus?.FocusChanged(false);
            activeMouseFocus = mouseFocus;
            activeMouseFocus?.FocusChanged(true);
        }

        public void SetDefaultKeyboardFocus(IKeyboardFocus focus)
        {
            defaultKeyboardFocus = focus;
        }

        public bool control => (keyMod & SDL.SDL_Keymod.KMOD_CTRL) != 0;

        internal void KeyDown(SDL.SDL_Keysym key)
        {
            keyMod = key.mod;
            (activeKeyboardFocus ?? defaultKeyboardFocus)?.KeyDown(key);
        }

        internal void KeyUp(SDL.SDL_Keysym key)
        {
            keyMod = key.mod;
            (activeKeyboardFocus ?? defaultKeyboardFocus)?.KeyUp(key);
        }

        internal void TextInput(string input)
        {
            (activeKeyboardFocus ?? defaultKeyboardFocus)?.TextInput(input);
        }

        internal void MouseScroll(int delta)
        {
            HitTest()?.MouseScroll(delta);
        }

        internal void MouseMove(int rawX, int rawY)
        {
            if (mouseOverWindow == null)
                return;
            var newMousePos = new Vector2(rawX / mouseOverWindow.pixelsPerUnit, rawY / mouseOverWindow.pixelsPerUnit);
            mouseDelta = newMousePos - mousePosition;
            mousePosition = newMousePos;
            if (mouseDownButton != -1 && mouseDownPanel != null)
                mouseDownPanel.MouseMove(mouseDownButton);
            else if (hoveringPanel != null)
                hoveringPanel?.MouseMove(-1);
        }
        
        internal void MouseExitWindow(Window window)
        {
            if (mouseOverWindow == window)
                mouseOverWindow = null;
        }

        internal void MouseEnterWindow(Window window)
        {
            mouseOverWindow = window;
        }

        public IPanel HitTest() => mouseOverWindow?.HitTest(mousePosition);

        internal void Update()
        {
            var currentHovering = HitTest();
            if (currentHovering != hoveringPanel)
            {
                hoveringPanel?.MouseExit();
                hoveringPanel = currentHovering;
            }
        }

        internal void MouseDown(int button)
        {
            if (mouseDownButton != -1)
                return;
            if (button == SDL.SDL_BUTTON_LEFT)
            {
                GlobalMouseDown?.Invoke(mouseOverWindow, hoveringPanel, mousePosition);
                if (activeKeyboardFocus != null)
                    SetKeyboardFocus(null);
                if (activeMouseFocus != null && !activeMouseFocus.FilterPanel(hoveringPanel))
                    SetMouseFocus((IMouseFocus) null);
            }

            mouseDownPosition = mousePosition;
            mouseDownPanel = hoveringPanel;
            mouseDownButton = button;
            mouseDownPanel?.MouseDown(button);
        }

        internal void MouseUp(int button)
        {
            if (button != mouseDownButton)
                return;
            if (mouseDownPanel != null && mouseDownPanel.valid)
            {
                mouseDownPanel.MouseUp(button);
                mouseDownPanel = null;
            }

            mouseDownPosition = default;
            mouseDownButton = -1;
            foreach (var mouseUp in mouseUpCallbacks)
                Ui.DispatchInMainThread(mouseUp.Item1, mouseUp.Item2);
            mouseUpCallbacks.Clear();
        }
    }
}