using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using SDL2;

namespace YAFC.UI
{
    public abstract class Window : IDisposable
    {
        public readonly ImGui rootGui;
        internal IntPtr window;
        protected internal IntPtr renderer;
        internal Vector2 contentSize;
        internal uint id;
        internal bool repaintRequired = true;
        internal bool visible;
        internal bool closed;
        internal long nextRepaintTime = long.MaxValue;
        internal static RenderingUtils.BlitMapping[] blitMapping;
        internal float pixelsPerUnit;
        public virtual SchemeColor backgroundColor => SchemeColor.Background;

        private Tooltip tooltip;
        private SimpleTooltip simpleTooltip;
        private DropDownPanel dropDown;
        private SimpleDropDown simpleDropDown;
        private ImGui.DragOverlay draggingOverlay;

        public int displayIndex => SDL.SDL_GetWindowDisplayIndex(window);
        public int repaintCount { get; private set; }

        public Vector2 size => contentSize;

        public virtual bool preventQuit => false;
        internal Window(Padding padding)
        {
            rootGui = new ImGui(Build, padding);
        }
        
        internal void Create()
        {
            SDL.SDL_SetRenderDrawBlendMode(renderer, SDL.SDL_BlendMode.SDL_BLENDMODE_BLEND);
            id = SDL.SDL_GetWindowID(window);
            Ui.RegisterWindow(id, this);
            visible = true;
        }

        internal float UnitsToPixelsFromDpi(float dpi) => dpi == 0 ? 13 : MathUtils.Round(dpi / 6.8f);

        internal virtual void WindowResize()
        {
            rootGui.Rebuild();
        }

        internal void WindowMoved()
        {
            var index = SDL.SDL_GetWindowDisplayIndex(window);
            SDL.SDL_GetDisplayDPI(index, out var ddpi, out _, out _);
            var u2p = UnitsToPixelsFromDpi(ddpi);
            if (u2p != pixelsPerUnit)
            {
                pixelsPerUnit = u2p;
                repaintRequired = true;
                rootGui.MarkEverythingForRebuild();
            }
        }
        
        protected virtual void OnRepaint() {}

        internal void Render()
        {
            if (!repaintRequired && nextRepaintTime > Ui.time)
                return;
            if (nextRepaintTime <= Ui.time)
                nextRepaintTime = long.MaxValue;
            OnRepaint();
            repaintRequired = false;
            if (rootGui.IsRebuildRequired())
                rootGui.CalculateState(size.X, pixelsPerUnit);

            MainRender();
            SDL.SDL_RenderPresent(renderer);
        }

        protected IntPtr RenderToTexture(out SDL.SDL_Rect textureSize)
        {
            SDL.SDL_GetRendererOutputSize(renderer, out var w, out var h);
            textureSize = new SDL.SDL_Rect {w = w, h = h};
            var texture = SDL.SDL_CreateTexture(renderer, SDL.SDL_PIXELFORMAT_RGBA8888, (int) SDL.SDL_TextureAccess.SDL_TEXTUREACCESS_TARGET, textureSize.w, textureSize.h);
            SDL.SDL_SetRenderTarget(renderer, texture);
            MainRender();
            SDL.SDL_SetRenderTarget(renderer, IntPtr.Zero);
            return texture;
        }

        internal virtual void MainRender()
        {
            var bgColor = backgroundColor.ToSdlColor();
            SDL.SDL_SetRenderDrawColor(renderer, bgColor.r,bgColor.g,bgColor.b, bgColor.a);
            var fullRect = new Rect(default, contentSize);
            repaintCount++;
            clipRect = rootGui.ToSdlRect(fullRect);
            {
                // TODO work-around sdl bug
                SDL.SDL_RenderSetClipRect(renderer, ref clipRect);
            }
            SDL.SDL_RenderClear(renderer);
            rootGui.InternalPresent(this, fullRect, fullRect);
        }

        private SDL.SDL_Rect clipRect;
        
        public IPanel HitTest(Vector2 position) => rootGui.HitTest(position);
        internal abstract void DrawIcon(SDL.SDL_Rect position, Icon icon, SchemeColor color);
        internal abstract void DrawBorder(SDL.SDL_Rect position, RectangleBorder border);
        public void Rebuild() => rootGui.Rebuild();

        public virtual SDL.SDL_Rect SetClip(SDL.SDL_Rect clip)
        {
            var prev = clipRect;
            clipRect = clip;
            SDL.SDL_RenderSetClipRect(renderer, ref clip);
            return prev;
        }

        public void Repaint()
        {
            if (closed)
                return;
            if (!Ui.IsMainThread())
                throw new NotSupportedException("This should be called from the main thread");
            repaintRequired = true;
        }

        protected internal virtual void Close()
        {
            visible = false;
            closed = true;
            SDL.SDL_DestroyWindow(window);
            Dispose();
            window = renderer = IntPtr.Zero;
            Ui.UnregisterWindow(this);
        }

        private void Focus()
        {
            if (window != IntPtr.Zero)
            {
                SDL.SDL_RaiseWindow(window);
                SDL.SDL_RestoreWindow(window);
                SDL.SDL_SetWindowInputFocus(window);
            }
        }

        
        public virtual void FocusLost() {}

        public void SetNextRepaint(long nextRepaintTime)
        {
            if (this.nextRepaintTime > nextRepaintTime)
                this.nextRepaintTime = nextRepaintTime;
        }

        public void ShowTooltip(Tooltip tooltip)
        {
            this.tooltip = tooltip;
            Rebuild();
        }

        public void ShowTooltip(ImGui targetGui, Rect target, GuiBuilder builder, float width = 20f)
        {
            if (simpleTooltip == null)
                simpleTooltip = new SimpleTooltip();
            simpleTooltip.Show(builder, targetGui, target, width);
            ShowTooltip(simpleTooltip);
            
        }

        public void ShowDropDown(DropDownPanel dropDown)
        {
            this.dropDown = dropDown;
            Rebuild();
        }

        public void ShowDropDown(ImGui targetGui, Rect target, SimpleDropDown.Builder builder, Padding padding, float width = 20f)
        {
            if (simpleDropDown == null)
                simpleDropDown = new SimpleDropDown();
            simpleDropDown.SetPadding(padding);
            simpleDropDown.SetFocus(targetGui, target, builder, width);
            ShowDropDown(simpleDropDown);
        }

        private void Build(ImGui gui)
        {
            if (closed)
                return;
            BuildContents(gui);
            if (dropDown != null)
            {
                dropDown.Build(gui);
                if (!dropDown.active)
                    dropDown = null;
            }
            draggingOverlay?.Build(gui);
            if (tooltip != null)
            {
                tooltip.Build(gui);
                if (!tooltip.active)
                    tooltip = null;
            }
        }

        protected abstract void BuildContents(ImGui gui);
        public virtual void Dispose()
        {
            rootGui.Dispose();
        }

        internal ImGui.DragOverlay GetDragOverlay()
        {
            return draggingOverlay ?? (draggingOverlay = new ImGui.DragOverlay());
        }
    }
}