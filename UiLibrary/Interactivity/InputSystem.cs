using System;
using System.Drawing;
using SDL2;

namespace UI
{
    internal sealed class InputSystem
    {
        private readonly RenderBatch batch;
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
        private bool mousePresent;

        public InputSystem(RenderBatch batch)
        {
            this.batch = batch;
        }

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

        public void KeyDown(SDL.SDL_Keysym key)
        {
            (activeKeyboardFocus ?? defaultKeyboardFocus)?.KeyDown(key);
        }

        public void KeyUp(SDL.SDL_Keysym key)
        {
            (activeKeyboardFocus ?? defaultKeyboardFocus)?.KeyUp(key);
        }

        public void TextInput(string input)
        {
            (activeKeyboardFocus ?? defaultKeyboardFocus)?.TextInput(input);
        }

        public void MouseScroll(int delta)
        {
            Raycast<IMouseScrollHandle>()?.Scroll(delta);
        }

        public void MouseMove(int rawX, int rawY)
        {
            position = new PointF(rawX / RenderingUtils.pixelsPerUnit, rawY / RenderingUtils.pixelsPerUnit);
        }
        
        public void MouseExitWindow()
        {
            mousePresent = false;
        }

        public void MouseEnterWindow()
        {
            mousePresent = true;
        }

        private T Raycast<T>() where T : class, IMouseHandle => mousePresent ? batch.Raycast<T>(position) : null;

        public void Update()
        {
            var newHoverObject = Raycast<IMouseEnterHandle>();
            if (newHoverObject != hoveringObject)
            {
                hoveringObject?.MouseExit();
                hoveringObject = newHoverObject;
                hoveringObject?.MouseEnter();
            }

            if (dragging)
            {
                mouseDragObject.Drag(Raycast<IMouseDropHandle>());
            } 
            else if (mouseDownObject != null)
            {
                if (mouseDragObject != null && MathF.Max(MathF.Abs(position.X-mouseDownPosition.X), MathF.Abs(position.Y - mouseDownPosition.Y)) >= 1f)
                {
                    dragging = true;
                    mouseDragObject.BeginDrag();
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
        }

        public void MouseDown(int button)
        {
            if (mouseDownButton == button)
                return;
            if (button == 0)
                SetKeyboardFocus(null);
            if (mouseDownButton != -1)
            {
                ClearMouseDownState();
                mouseDragObject = null;
            }
            mouseDownButton = button;
            mouseDownObject = Raycast<IMouseClickHandle>();
            if (button == 0)
                mouseDragObject = Raycast<IMouseDragHandle>();
            mouseDownPosition = position;
            if (mouseDownObject != null)
            {
                mouseDownObjectActive = true;
                mouseDownObject.MouseClickUpdateState(true, button);
            }
        }

        public void MouseUp(int button)
        {
            if (button != mouseDownButton)
                return;
            if (dragging)
            {
                var drop = Raycast<IMouseDropHandle>();
                mouseDragObject.EndDrag(drop);
                dragging = false;
            } 
            else if (mouseDownObjectActive)
            {
                mouseDownObject.MouseClick(mouseDownButton);
            }

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