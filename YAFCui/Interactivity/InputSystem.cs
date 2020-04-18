using System.Diagnostics;
using System.Drawing;
using SDL2;

namespace YAFC.UI
{
    public sealed class InputSystem
    {
        public static readonly InputSystem Instance = new InputSystem();
        
        private InputSystem() {}

        private Window mouseOverWindow;
        private IMouseEnterHandle hoveringObject;
        private UiBatch hoveringBatch;
        private IMouseClickHandle mouseDownObject;
        private UiBatch mouseDownBatch;
        private IMouseDragHandle mouseDragObject;
        private UiBatch mouseDragBatch;
        private IKeyboardFocus activeKeyboardFocus;
        private IKeyboardFocus defaultKeyboardFocus;
        private bool mouseDownObjectActive;
        private int mouseDownButton = -1;
        private PointF position;

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
            if (Raycast<IMouseScrollHandle>(out var result, out var batch))
                result.Scroll(delta, batch);
        }

        internal void MouseMove(int rawX, int rawY)
        {
            position = new PointF(rawX / mouseOverWindow.rootBatch.pixelsPerUnit, rawY / mouseOverWindow.rootBatch.pixelsPerUnit);
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

        public bool Raycast<T>(out T result, out UiBatch batch) where T : class, IMouseHandle
        {
            if (mouseOverWindow != null)
                return mouseOverWindow.Raycast(position, out result, out batch);
            result = null;
            batch = null;
            return false;
        }

        internal void Update()
        {
            Raycast<IMouseEnterHandle>(out var newHoverObject, out var batch);
            if (newHoverObject != hoveringObject)
            {
                hoveringObject?.MouseExit(hoveringBatch);
                hoveringObject = newHoverObject;
                hoveringBatch = batch;
                hoveringObject?.MouseEnter(batch);
            }
            
            if (mouseDragObject != null)
            {
                mouseDragObject.Drag(position, mouseDragBatch);
            } 
            else if (mouseDownObject != null)
            {
                Raycast(out IMouseClickHandle clickHandle, out _);
                var shouldActive = mouseDownObject == clickHandle;
                if (shouldActive != mouseDownObjectActive)
                {
                    mouseDownObject.MouseClickUpdateState(shouldActive, mouseDownButton, mouseDownBatch);
                    mouseDownObjectActive = shouldActive;
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
            if (button == SDL.SDL_BUTTON_LEFT)
            {
                if (Raycast(out mouseDragObject, out mouseDragBatch))
                {
                    ClearMouseDownState();
                    mouseDragObject?.MouseDown(position, mouseDragBatch);
                }
            }

            if (mouseDragObject == null)
            {
                if (Raycast(out mouseDownObject, out mouseDownBatch))
                {
                    mouseDownObjectActive = true;
                    mouseDownObject.MouseClickUpdateState(true, button, mouseDownBatch);
                }
            }
        }

        internal void MouseUp(int button)
        {
            if (button != mouseDownButton)
                return;
            if (mouseDragObject != null)
            {
                mouseDragObject.EndDrag(position, mouseDragBatch);
            } 
            else if (mouseDownObjectActive)
            {
                mouseDownObject.MouseClick(mouseDownButton, mouseDownBatch);
            }

            mouseDownButton = -1;
            ClearMouseDownState();
            mouseDragObject = null;
            mouseDragBatch = null;
        }

        private void ClearMouseDownState()
        {
            if (mouseDownObjectActive)
            {
                mouseDownObjectActive = false;
                mouseDownObject?.MouseClickUpdateState(false, mouseDownButton, mouseDownBatch);
            }
            mouseDownObject = null;
            mouseDownBatch = null;
        }
    }
}