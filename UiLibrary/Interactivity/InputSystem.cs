using System;
using System.Diagnostics;
using System.Drawing;
using SDL2;

namespace UI
{
    public sealed class InputSystem
    {
        public static readonly InputSystem Instance = new InputSystem();
        public static long time { get; private set; }
        private readonly Stopwatch timeWatch = Stopwatch.StartNew();
        
        private InputSystem() {}

        private Window mouseOverWindow;
        private IMouseEnterHandle hoveringObject;
        private IMouseClickHandle mouseDownObject;
        private IMouseDragHandle mouseDragObject;
        private IKeyboardFocus activeKeyboardFocus;
        private IKeyboardFocus defaultKeyboardFocus;
        private bool mouseDownObjectActive;
        private int mouseDownButton = -1;
        private bool dragging;
        private PointF position;
        private PointF mouseDownPosition;

        private IKeyboardFocus currentKeyboardFocus => activeKeyboardFocus ?? defaultKeyboardFocus; 

        public void SetKeyboardFocus(IKeyboardFocus focus)
        {
            if (focus == activeKeyboardFocus)
                return;
            currentKeyboardFocus?.FocusChanged(false);
            activeKeyboardFocus = focus;
            currentKeyboardFocus?.FocusChanged(true);
        }

        public void SetDefaultKeyboardFocus(IKeyboardFocus focus)
        {
            defaultKeyboardFocus = focus;
        }

        internal void KeyDown(SDL.SDL_Keysym key)
        {
            (activeKeyboardFocus ?? defaultKeyboardFocus)?.KeyDown(key);
        }

        internal void KeyUp(SDL.SDL_Keysym key)
        {
            (activeKeyboardFocus ?? defaultKeyboardFocus)?.KeyUp(key);
        }

        internal void TextInput(string input)
        {
            (activeKeyboardFocus ?? defaultKeyboardFocus)?.TextInput(input);
        }

        internal void MouseScroll(int delta)
        {
            Raycast<IMouseScrollHandle>()?.Scroll(delta);
        }

        internal void MouseMove(int rawX, int rawY)
        {
            position = new PointF(rawX / RenderingUtils.pixelsPerUnit, rawY / RenderingUtils.pixelsPerUnit);
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

        private T Raycast<T>() where T : class, IMouseHandle => mouseOverWindow?.Raycast<T>(position);

        internal void Update()
        {
            time = timeWatch.ElapsedMilliseconds;
            var newHoverObject = Raycast<IMouseEnterHandle>();
            if (newHoverObject != hoveringObject)
            {
                hoveringObject?.MouseExit();
                hoveringObject = newHoverObject;
                hoveringObject?.MouseEnter();
            }

            if (dragging)
            {
                mouseDragObject.Drag(position, Raycast<IMouseDropHandle>());
            } 
            else if (mouseDownObject != null)
            {
                if (mouseDragObject != null && MathF.Max(MathF.Abs(position.X-mouseDownPosition.X), MathF.Abs(position.Y - mouseDownPosition.Y)) >= 1f)
                {
                    dragging = true;
                    mouseDragObject.BeginDrag(position);
                    ClearMouseDownState();
                }
                else
                {
                    var clickHandle = Raycast<IMouseClickHandle>();
                    var shouldActive = mouseDownObject == clickHandle;
                    if (shouldActive != mouseDownObjectActive)
                    {
                        mouseDownObject.MouseClickUpdateState(shouldActive, mouseDownButton);
                        mouseDownObjectActive = shouldActive;
                    }
                }
            }

            activeKeyboardFocus?.UpdateSelected();
        }

        internal void MouseDown(int button)
        {
            if (mouseDownButton == button)
                return;
            if (button == SDL.SDL_BUTTON_LEFT)
                SetKeyboardFocus(null);
            if (mouseDownButton != -1)
            {
                ClearMouseDownState();
                mouseDragObject = null;
            }
            mouseDownButton = button;
            mouseDownObject = Raycast<IMouseClickHandle>();
            if (button == SDL.SDL_BUTTON_LEFT)
            {
                mouseDragObject = Raycast<IMouseDragHandle>();
                mouseDragObject?.MouseDown(position);
            }
            mouseDownPosition = position;
            if (mouseDownObject != null)
            {
                mouseDownObjectActive = true;
                mouseDownObject.MouseClickUpdateState(true, button);
            }
        }

        internal void MouseUp(int button)
        {
            if (button != mouseDownButton)
                return;
            if (dragging)
            {
                var drop = Raycast<IMouseDropHandle>();
                mouseDragObject.EndDrag(position, drop);
                dragging = false;
            } 
            else if (mouseDownObjectActive)
            {
                mouseDownObject.MouseClick(mouseDownButton);
            }

            mouseDownButton = -1;
            ClearMouseDownState();
            mouseDragObject = null;
        }

        private void ClearMouseDownState()
        {
            if (mouseDownObjectActive)
            {
                mouseDownObjectActive = false;
                mouseDownObject?.MouseClickUpdateState(false, mouseDownButton);
            }
            mouseDownObject = null;
        }
    }
}