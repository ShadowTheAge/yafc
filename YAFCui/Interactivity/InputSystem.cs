using System.Numerics;
using SDL2;

namespace YAFC.UI
{
    public readonly struct RaycastResult<T>
    {
        public readonly T target;
        public readonly UiBatch owner;
        public readonly Rect rect;

        public RaycastResult(T target, UiBatch owner, Rect rect)
        {
            this.target = target;
            this.owner = owner;
            this.rect = rect;
        }
    }
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
        private Vector2 position;

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
            if (Raycast<IMouseScrollHandle>(out var result))
                result.target.Scroll(delta, result.owner);
        }

        internal void MouseMove(int rawX, int rawY)
        {
            position = new Vector2(rawX / mouseOverWindow.rootBatch.pixelsPerUnit, rawY / mouseOverWindow.rootBatch.pixelsPerUnit);
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

        public bool Raycast<T>(out RaycastResult<T> result) where T : class, IMouseHandle
        {
            if (mouseOverWindow != null)
                return mouseOverWindow.Raycast(position, out result);
            result = default;
            return false;
        }

        internal void Update()
        {
            Raycast<IMouseEnterHandle>(out var newHoverObject);
            if (newHoverObject.target != hoveringObject)
            {
                hoveringObject?.MouseExit(hoveringBatch);
                hoveringObject = newHoverObject.target;
                hoveringBatch = newHoverObject.owner;
                hoveringObject?.MouseEnter(newHoverObject);
            }
            
            if (mouseDragObject != null)
            {
                mouseDragObject.Drag(position, mouseDragBatch);
            } 
            else if (mouseDownObject != null)
            {
                Raycast<IMouseClickHandle>(out var clickHandle);
                var shouldActive = mouseDownObject == clickHandle.target;
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
                if (Raycast<IMouseDragHandle>(out var dragResult))
                {
                    mouseDragObject = dragResult.target;
                    mouseDragBatch = dragResult.owner;
                    ClearMouseDownState();
                    mouseDragObject?.MouseDown(position, mouseDragBatch);
                }
            }

            if (mouseDragObject == null)
            {
                if (Raycast<IMouseClickHandle>(out var result))
                {
                    mouseDownObject = result.target;
                    mouseDownBatch = result.owner;
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