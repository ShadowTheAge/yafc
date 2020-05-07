using System;
using System.Collections.Generic;
using System.Numerics;

namespace YAFC.UI
{
    public partial class ImGui
    {
        private object currentDraggingObject;
        
        public void SetDraggingArea<T>(Rect rect, T draggingObject, SchemeColor bgColor)
        {
            if (window == null || mouseDownButton == -1)
                return;
            rebuildRequested = false;
            currentDraggingObject = draggingObject;
            var overlay = window.GetDragOverlay();
            overlay.BeginDrag(this, rect, bgColor);
        }

        public void UpdateDraggingObject(object obj)
        {
            if (currentDraggingObject != null)
                currentDraggingObject = obj;
        }

        public bool IsDragging<T>(T obj)
        {
            if (currentDraggingObject != null && obj.Equals(currentDraggingObject))
                return true;
            return false;
        }

        public bool ConsumeDrag<T>(Rect rect, T obj, bool changeDraggingObject = true)
        {
            if (action == ImGuiAction.MouseDrag && rect.Contains(mousePosition) && currentDraggingObject != null && !obj.Equals(currentDraggingObject))
            {
                action = ImGuiAction.Consumed;
                Rebuild();
                return true;
            }

            return false;
        }

        public T GetDraggingObject<T>() => currentDraggingObject is T t ? t : default;
        
        internal class DragOverlay
        {
            private readonly ImGui contents = new ImGui(null, default) {mouseCapture = false};

            private ImGui currentSource;
            private Vector2 mouseOffset;
            
            private void ExtractDrawCommandsFrom<T>(List<DrawCommand<T>> sourceList, List<DrawCommand<T>> targetList, Rect rect)
            {
                targetList.Clear();
                var delta = rect.Position;
                var firstInBlock = -1;
                for (var i = 0; i < sourceList.Count; i++)
                {
                    var elem = sourceList[i];
                    if (rect.Contains(elem.rect))
                    {
                        if (firstInBlock == -1)
                            firstInBlock = i;
                        targetList.Add(new DrawCommand<T>(elem.rect - delta, elem.data, elem.color));
                    } 
                    else if (firstInBlock != -1)
                    {
                        sourceList.RemoveRange(firstInBlock, i-firstInBlock);
                        i = firstInBlock;
                        firstInBlock = -1;
                    }
                }
                if (firstInBlock != -1)
                    sourceList.RemoveRange(firstInBlock, sourceList.Count-firstInBlock);
            }
            
            public void BeginDrag(ImGui source, Rect rect, SchemeColor bgColor)
            {
                if (source != currentSource)
                {
                    currentSource = source;
                    mouseOffset = rect.Position - source.mousePosition;
                }
                ExtractDrawCommandsFrom(source.rects, contents.rects, rect);
                ExtractDrawCommandsFrom(source.icons, contents.icons, rect);
                ExtractDrawCommandsFrom(source.renderables, contents.renderables, rect);
                ExtractDrawCommandsFrom(source.panels, contents.panels, rect);
                contents.rects.Add(new DrawCommand<RectangleBorder>(new Rect(default, rect.Size), RectangleBorder.Thin, bgColor));
                contents.contentSize = rect.Size;
            }
            
            public void Build(ImGui screenGui)
            {
                if (currentSource == null)
                    return;
                if (InputSystem.Instance.mouseDownButton == -1)
                {
                    currentSource = null;
                    return;
                }

                if (screenGui.action == ImGuiAction.Build)
                {
                    var sourceRect = currentSource.screenRect - currentSource.offset;
                    var requestedPosition = screenGui.mousePosition + mouseOffset;
                    var clampedPos = Vector2.Clamp(requestedPosition, sourceRect.Position, Vector2.Max(sourceRect.Position, sourceRect.BottomRight - contents.contentSize));
                    screenGui.DrawPanel(new Rect(clampedPos, currentSource.contentSize), contents);
                }
            }
        }
    }
}