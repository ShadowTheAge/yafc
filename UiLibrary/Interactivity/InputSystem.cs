using System;
using System.Drawing;

namespace UI
{
    internal sealed class InputSystem
    {
        private readonly RenderBatch batch;
        private IMouseEnterHandle hoveringObject;
        private IMouseClickHandle mouseDownObject;
        private IMouseDragHandle mouseDragObject;
        private bool mouseDownObjectActive;
        private int mouseDownButton = -1;
        private bool dragging;
        private PointF position;
        private PointF mouseDownPosition;

        public InputSystem(RenderBatch batch)
        {
            this.batch = batch;
        }

        public void Update(PointF mousePosition)
        {
            position = mousePosition;
            var newHoverObject = batch.Raycast<IMouseEnterHandle>(position);
            if (newHoverObject != hoveringObject)
            {
                hoveringObject?.MouseExit();
                hoveringObject = newHoverObject;
                hoveringObject?.MouseEnter();
            }

            if (dragging)
            {
                mouseDragObject.Drag(batch.Raycast<IMouseDropHandle>(position));
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
                    var clickHandle = batch.Raycast<IMouseClickHandle>(position);
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
            if (mouseDownButton != -1)
            {
                ClearMouseDownState();
                mouseDragObject = null;
            }
            mouseDownButton = button;
            mouseDownObject = batch.Raycast<IMouseClickHandle>(position);
            if (button == 0)
                mouseDragObject = batch.Raycast<IMouseDragHandle>(position);
            mouseDownPosition = position;
            if (mouseDownObject != null)
            {
                mouseDownObjectActive = true;
                mouseDownObject.MouseClickUpdateState(true, button);
            }
        }

        public void MouseUp(int button)
        {
            if (dragging)
            {
                var drop = batch.Raycast<IMouseDropHandle>(position);
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