using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace Yafc.UI {
    public partial class ImGui {
        private object? currentDraggingObject;

        public void SetDraggingArea<T>(Rect rect, T draggingObject, SchemeColor bgColor) {
            if (window == null || mouseDownButton == -1) {
                return;
            }

            rebuildRequested = false;
            currentDraggingObject = draggingObject;
            var overlay = window.GetDragOverlay();
            overlay.BeginDrag(this, rect, bgColor);
        }

        public void UpdateDraggingObject(object? obj) {
            if (currentDraggingObject != null) {
                currentDraggingObject = obj;
            }
        }

        public bool isDragging => currentDraggingObject != null;

        public bool IsDragging<T>(T obj) {
            if (currentDraggingObject != null && currentDraggingObject.Equals(obj)) {
                return true;
            }

            return false;
        }

        [MemberNotNullWhen(true, nameof(currentDraggingObject))]
        public bool ConsumeDrag<T>(Vector2 anchor, T obj) {
            if (window == null || mouseDownButton == -1) {
                return false;
            }

            if (action == ImGuiAction.MouseDrag && currentDraggingObject != null && !currentDraggingObject.Equals(obj) && window.GetDragOverlay().ShouldConsumeDrag(this, anchor)) {
                action = ImGuiAction.Consumed;
                Rebuild();
                return true;
            }

            return false;
        }

        public T? GetDraggingObject<T>() => currentDraggingObject is T t ? t : default;

        public bool DoListReordering<T>(Rect moveHandle, Rect contents, T index, out T moveFrom, SchemeColor backgroundColor = SchemeColor.PureBackground, bool updateDraggingObject = true) {
            moveFrom = index;
            if (!this.InitiateDrag(moveHandle, contents, index, backgroundColor) && action == ImGuiAction.MouseDrag && ConsumeDrag(contents.Center, index)) {
                moveFrom = (T)currentDraggingObject;
                if (updateDraggingObject) {
                    UpdateDraggingObject(index);
                }

                return true;
            }
            return false;
        }


        internal class DragOverlay {
            private readonly ImGui contents = new ImGui(null, default) { mouseCapture = false };

            private ImGui? currentSource;
            private Vector2 mouseOffset;
            private Rect realPosition;


            public bool ShouldConsumeDrag(ImGui source, Vector2 point) => currentSource == source && realPosition.Contains(source.ToWindowPosition(point));

            private void ExtractDrawCommandsFrom<T>(List<DrawCommand<T>> sourceList, List<DrawCommand<T>> targetList, Rect rect) {
                targetList.Clear();
                var delta = rect.Position;
                int firstInBlock = -1;
                for (int i = 0; i < sourceList.Count; i++) {
                    var elem = sourceList[i];
                    if (rect.Contains(elem.rect)) {
                        if (firstInBlock == -1) {
                            firstInBlock = i;
                        }

                        targetList.Add(new DrawCommand<T>(elem.rect - delta, elem.data, elem.color));
                    }
                    else if (firstInBlock != -1) {
                        sourceList.RemoveRange(firstInBlock, i - firstInBlock);
                        i = firstInBlock;
                        firstInBlock = -1;
                    }
                }
                if (firstInBlock != -1) {
                    sourceList.RemoveRange(firstInBlock, sourceList.Count - firstInBlock);
                }
            }

            public void BeginDrag(ImGui source, Rect rect, SchemeColor bgColor) {
                if (source != currentSource) {
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

            public void Build(ImGui screenGui) {
                if (currentSource == null) {
                    return;
                }

                if (InputSystem.Instance.mouseDownButton == -1) {
                    currentSource = null;
                    realPosition = default;
                    return;
                }

                if (screenGui.action == ImGuiAction.Build) {
                    var sourceRect = currentSource.screenRect - currentSource.offset;
                    var requestedPosition = screenGui.mousePosition + mouseOffset;
                    Vector2 clampedPos = Vector2.Clamp(requestedPosition, sourceRect.Position, Vector2.Max(sourceRect.Position, sourceRect.BottomRight - contents.contentSize));
                    realPosition = new Rect(clampedPos, contents.contentSize);
                    screenGui.DrawPanel(realPosition, contents);
                }
            }
        }

        public bool ShouldEnterDrag(Rect moveHandle) {
            if (action != ImGuiAction.MouseMove || !IsMouseDown(moveHandle) || isDragging || Vector2.DistanceSquared(InputSystem.Instance.mousePosition, InputSystem.Instance.mouseDownPosition) < 1f) {
                return false;
            }

            return true;
        }
    }
}
