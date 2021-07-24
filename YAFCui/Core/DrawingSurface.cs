using System;
using SDL2;

namespace YAFC.UI
{
    public abstract class DrawingSurface : IDisposable
    {
        public IntPtr renderer { get; protected set; }
        public float pixelsPerUnit { get; set; }

        protected DrawingSurface(float pixelsPerUnit)
        {
            this.pixelsPerUnit = pixelsPerUnit;
        }

        internal static RenderingUtils.BlitMapping[] blitMapping;
        
        private SDL.SDL_Rect clipRect;
        internal abstract void DrawIcon(SDL.SDL_Rect position, Icon icon, SchemeColor color);
        internal abstract void DrawBorder(SDL.SDL_Rect position, RectangleBorder type);
        
        public abstract Window window { get; }

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

    public abstract class SoftwareDrawingSurface : DrawingSurface
    {
        protected IntPtr surface;

        protected SoftwareDrawingSurface(IntPtr surface, float pixelsPerUnit) : base(pixelsPerUnit)
        {
            this.surface = surface;
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
            RenderingUtils.GetBorderParameters(pixelsPerUnit, border, out var top, out var side, out var bottom);
            RenderingUtils.GetBorderBatch(position, top, side, bottom, ref blitMapping);
            var bm = blitMapping;
            for (var i = 0; i < bm.Length; i++)
            {
                ref var cur = ref bm[i];
                SDL.SDL_BlitScaled(RenderingUtils.CircleSurface, ref cur.texture, surface, ref cur.position);
            }
        }
    }

    public class MemoryDrawingSurface : SoftwareDrawingSurface
    {
        public MemoryDrawingSurface(int width, int height, float pixlesPerUnit) : base(SDL.SDL_CreateRGBSurfaceWithFormat(0, width, height, 0, SDL.SDL_PIXELFORMAT_RGB888), pixlesPerUnit) {}

        public override void Dispose()
        {
            base.Dispose();
            SDL.SDL_FreeSurface(surface);
            surface = IntPtr.Zero;
        }
        
        public override Window window => null;
    }
}