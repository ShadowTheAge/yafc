using System;
using System.Numerics;
using SDL2;

namespace Yafc.UI;

public readonly struct TextureHandle(DrawingSurface surface, IntPtr handle) {
    public readonly IntPtr handle = handle;
    public readonly DrawingSurface surface = surface;
    public readonly int version = surface.rendererVersion;
    public bool valid => surface != null && surface.rendererVersion == version;

    public TextureHandle Destroy() {
        if (valid) {
            nint capturedHandle = handle;
            Ui.DispatchInMainThread(_ => SDL.SDL_DestroyTexture(capturedHandle), null);
        }
        return default;
    }
}

public abstract class DrawingSurface(float pixelsPerUnit) : IDisposable {
    private IntPtr rendererHandle;

    public IntPtr renderer {
        get => rendererHandle;
        protected set {
            rendererHandle = value;
            rendererVersion++;
        }
    }

    public int rendererVersion { get; private set; }

    public float pixelsPerUnit { get; set; } = pixelsPerUnit;

    internal static RenderingUtils.BlitMapping[]? blitMapping;

    private SDL.SDL_Rect clipRect;
    private bool disposedValue;

    internal abstract void DrawIcon(SDL.SDL_Rect position, Icon icon, SchemeColor color);
    internal abstract void DrawBorder(SDL.SDL_Rect position, RectangleBorder type);

    public abstract Window? window { get; }

    public TextureHandle BeginRenderToTexture(out SDL.SDL_Rect textureSize) {
        _ = SDL.SDL_GetRendererOutputSize(renderer, out int w, out int h);
        textureSize = new SDL.SDL_Rect { w = w, h = h };
        nint texture = SDL.SDL_CreateTexture(renderer, SDL.SDL_PIXELFORMAT_RGBA8888, (int)SDL.SDL_TextureAccess.SDL_TEXTUREACCESS_TARGET, textureSize.w, textureSize.h);
        _ = SDL.SDL_SetRenderTarget(renderer, texture);

        return new TextureHandle(this, texture);
    }

    public void EndRenderToTexture() => _ = SDL.SDL_SetRenderTarget(renderer, IntPtr.Zero);

    public virtual SDL.SDL_Rect SetClip(SDL.SDL_Rect clip) {
        var prev = clipRect;
        clipRect = clip;
        _ = SDL.SDL_RenderSetClipRect(renderer, ref clip);

        return prev;
    }

    public virtual void Present() => SDL.SDL_RenderPresent(renderer);

    public void Clear(SDL.SDL_Rect clipRect) {
        this.clipRect = clipRect;
        {
            // TODO work-around sdl bug
            _ = SDL.SDL_RenderSetClipRect(renderer, ref clipRect);
        }
        _ = SDL.SDL_RenderClear(renderer);
    }

    public TextureHandle CreateTextureFromSurface(IntPtr surface) => new TextureHandle(this, SDL.SDL_CreateTextureFromSurface(renderer, surface));

    public TextureHandle CreateTexture(uint format, int access, int w, int h) => new TextureHandle(this, SDL.SDL_CreateTexture(renderer, format, access, w, h));

    protected virtual void Dispose(bool disposing) {
        if (!disposedValue) {
            if (disposing) {
                // dispose managed state (managed objects)
            }

            // free unmanaged resources (unmanaged objects) and override finalizer
            SDL.SDL_DestroyRenderer(renderer);
            renderer = IntPtr.Zero;
            // set large fields to null

            disposedValue = true;
        }
    }

    ~DrawingSurface() {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: false);
    }

    public virtual void Dispose() {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}

public abstract class SoftwareDrawingSurface(IntPtr surface, float pixelsPerUnit) : DrawingSurface(pixelsPerUnit) {
    public IntPtr surface { get; protected set; } = surface;

    public override SDL.SDL_Rect SetClip(SDL.SDL_Rect clip) {
        _ = SDL.SDL_SetClipRect(surface, ref clip);
        return base.SetClip(clip);
    }

    internal override void DrawIcon(SDL.SDL_Rect position, Icon icon, SchemeColor color) {
        var sdlColor = color.ToSdlColor();
        nint iconSurface = IconCollection.GetIconSurface(icon);
        _ = SDL.SDL_SetSurfaceColorMod(iconSurface, sdlColor.r, sdlColor.g, sdlColor.b);
        _ = SDL.SDL_SetSurfaceAlphaMod(iconSurface, sdlColor.a);
        _ = SDL.SDL_BlitScaled(iconSurface, ref IconCollection.IconRect, surface, ref position);
    }

    internal override void DrawBorder(SDL.SDL_Rect position, RectangleBorder border) {
        RenderingUtils.GetBorderParameters(pixelsPerUnit, border, out int top, out int side, out int bottom);
        RenderingUtils.GetBorderBatch(position, top, side, bottom, ref blitMapping);
        var bm = blitMapping;

        for (int i = 0; i < bm.Length; i++) {
            ref var cur = ref bm[i];
            _ = SDL.SDL_BlitScaled(RenderingUtils.CircleSurface, ref cur.texture, surface, ref cur.position);
        }
    }
}

public class MemoryDrawingSurface : SoftwareDrawingSurface {
    public MemoryDrawingSurface(Vector2 size, float pixelsPerUnit) : this(size, ClampPixelsPerUnit(size, pixelsPerUnit), true) { }

    private MemoryDrawingSurface(Vector2 size, float pixelsPerUnit, bool __) :
        base(SDL.SDL_CreateRGBSurfaceWithFormat(0, MathUtils.Round(size.X * pixelsPerUnit), MathUtils.Round(size.Y * pixelsPerUnit), 0, SDL.SDL_PIXELFORMAT_RGB888), pixelsPerUnit) {
        renderer = SDL.SDL_CreateSoftwareRenderer(surface);
        _ = SDL.SDL_SetRenderDrawBlendMode(renderer, SDL.SDL_BlendMode.SDL_BLENDMODE_BLEND);
    }

    public static float ClampPixelsPerUnit(Vector2 size, float pixelsPerUnit) {
        float maxPPU = MathF.Min(65535 / size.X, 65535 / size.Y);
        return MathF.Min(maxPPU, pixelsPerUnit);
    }

    public void Clear(SDL.SDL_Color bgColor) {
        _ = SDL.SDL_SetRenderDrawColor(renderer, bgColor.r, bgColor.g, bgColor.b, bgColor.a);
        _ = SDL.SDL_RenderClear(renderer);
    }

    public override void Dispose() {
        if (surface == IntPtr.Zero) {
            return;
        }

        base.Dispose();
        SDL.SDL_FreeSurface(surface);
        surface = IntPtr.Zero;
        GC.SuppressFinalize(this);
    }

    public override Window? window => null;

    public void SavePng(string filename) => _ = SDL_image.IMG_SavePNG(surface, filename);
}
