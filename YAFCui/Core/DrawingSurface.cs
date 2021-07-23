using System;
using SDL2;

namespace YAFC.UI
{
    public abstract class DrawingSurface : IDisposable
    {
        public Window window { get; }
        protected DrawingSurface(Window window)
        {
            this.window = window;
        }
        public IntPtr renderer { get; protected set; }
        public virtual bool valid => renderer != IntPtr.Zero;

        internal static RenderingUtils.BlitMapping[] blitMapping;
        
        private SDL.SDL_Rect clipRect;
        internal abstract void DrawIcon(SDL.SDL_Rect position, Icon icon, SchemeColor color);
        internal abstract void DrawBorder(SDL.SDL_Rect position, RectangleBorder type);

        public virtual void Dispose()
        {
            SDL.SDL_DestroyRenderer(renderer);
            renderer = IntPtr.Zero;
        }

        public IntPtr BeginRenderToTexture(out SDL.SDL_Rect textureSize)
        {
            SDL.SDL_GetRendererOutputSize(renderer, out var w, out var h);
            textureSize = new SDL.SDL_Rect {w = w, h = h};
            var texture = SDL.SDL_CreateTexture(renderer, SDL.SDL_PIXELFORMAT_RGBA8888, (int) SDL.SDL_TextureAccess.SDL_TEXTUREACCESS_TARGET, textureSize.w, textureSize.h);
            SDL.SDL_SetRenderTarget(renderer, texture);
            return texture;
        }

        public void EndRenderToTexture()
        {
            SDL.SDL_SetRenderTarget(renderer, IntPtr.Zero);
        }

        public virtual SDL.SDL_Rect SetClip(SDL.SDL_Rect clip)
        {
            var prev = clipRect;
            clipRect = clip;
            SDL.SDL_RenderSetClipRect(renderer, ref clip);
            return prev;
        }
        
        public virtual void OnResize() {}

        public virtual void Present()
        {
            SDL.SDL_RenderPresent(renderer);
        }

        public void Clear(SDL.SDL_Rect clipRect)
        {
            this.clipRect = clipRect;
            {
                // TODO work-around sdl bug
                SDL.SDL_RenderSetClipRect(renderer, ref clipRect);
            }
            SDL.SDL_RenderClear(renderer);
        }
    }

    public class SoftwareDrawingSurface : DrawingSurface
    {
        private IntPtr surface;

        private void InvalidateRenderer()
        {
            surface = SDL.SDL_GetWindowSurface(window.window);
            renderer = SDL.SDL_CreateSoftwareRenderer(surface);
            SDL.SDL_SetRenderDrawBlendMode(renderer, SDL.SDL_BlendMode.SDL_BLENDMODE_BLEND);
        }

        public override bool valid => base.valid && surface != IntPtr.Zero;

        public SoftwareDrawingSurface(Window window) : base(window)
        {
            InvalidateRenderer();
        }
        
        public override SDL.SDL_Rect SetClip(SDL.SDL_Rect clip)
        {
            SDL.SDL_SetClipRect(surface, ref clip);
            return base.SetClip(clip);
        }
        
        internal override void DrawIcon(SDL.SDL_Rect position, Icon icon, SchemeColor color)
        {
            var sdlColor = color.ToSdlColor();
            var iconSurface = IconCollection.GetIconSurface(icon);
            SDL.SDL_SetSurfaceColorMod(iconSurface, sdlColor.r, sdlColor.g, sdlColor.b);
            SDL.SDL_SetSurfaceAlphaMod(iconSurface, sdlColor.a);
            SDL.SDL_BlitScaled(iconSurface, ref IconCollection.IconRect, surface, ref position);
        }

        internal override void DrawBorder(SDL.SDL_Rect position, RectangleBorder border)
        {
            RenderingUtils.GetBorderParameters(window.pixelsPerUnit, border, out var top, out var side, out var bottom);
            RenderingUtils.GetBorderBatch(position, top, side, bottom, ref blitMapping);
            var bm = blitMapping;
            for (var i = 0; i < bm.Length; i++)
            {
                ref var cur = ref bm[i];
                SDL.SDL_BlitScaled(RenderingUtils.CircleSurface, ref cur.texture, surface, ref cur.position);
            }
        }

        public override void Dispose()
        {
            base.Dispose();
            surface = IntPtr.Zero;
        }

        public override void OnResize()
        {
            InvalidateRenderer();
            base.OnResize();
        }
    }

    public class MainWindowDrawingSurface : DrawingSurface
    {
        private IconAtlas atlas = new IconAtlas();
        private IntPtr circleTexture;
        
        public MainWindowDrawingSurface(WindowMain window) : base(window)
        {
            renderer = SDL.SDL_CreateRenderer(window.window, 0, SDL.SDL_RendererFlags.SDL_RENDERER_PRESENTVSYNC);
            circleTexture = SDL.SDL_CreateTextureFromSurface(renderer, RenderingUtils.CircleSurface);
            var colorMod = RenderingUtils.darkMode ? (byte) 255 : (byte) 0;
            SDL.SDL_SetTextureColorMod(circleTexture, colorMod, colorMod, colorMod);
        }

        internal override void DrawIcon(SDL.SDL_Rect position, Icon icon, SchemeColor color)
        {
            atlas.DrawIcon(renderer, icon, position, color.ToSdlColor());
        }

        internal override void DrawBorder(SDL.SDL_Rect position, RectangleBorder border)
        {
            RenderingUtils.GetBorderParameters(window.pixelsPerUnit, border, out var top, out var side, out var bottom);
            RenderingUtils.GetBorderBatch(position, top, side, bottom, ref blitMapping);
            var bm = blitMapping;
            for (var i = 0; i < bm.Length; i++)
            {
                ref var cur = ref bm[i];
                SDL.SDL_RenderCopy(renderer, circleTexture, ref cur.texture, ref cur.position);
            }
        }
    }
}