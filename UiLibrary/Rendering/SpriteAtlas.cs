using System;
using SDL2;

namespace UI
{
    internal sealed class SpriteAtlas : UnmanagedResource
    {
        private Sprite nextId = Sprite.FirstCustom;
        private readonly IntPtr targetSurface;

        private const int TextureSizeLog = 11;
        private const int SpriteSizeLog = 5;
        private const int SpriteSize = 1 << SpriteSizeLog;
        private const int SpritesPerRowLog = TextureSizeLog - SpriteSizeLog;
        private const int SpritesPerDimension = 1 << SpritesPerRowLog;
        private const int ColumnMask = SpritesPerDimension - 1;
        private static SDL.SDL_Rect TargetSurfaceRect = new SDL.SDL_Rect {w = SpriteSize, h = SpriteSize};

        public SpriteAtlas()
        {
            var size = 1 << TextureSizeLog;
            handle = SDL.SDL_CreateTexture(RenderingUtils.renderer, SDL.SDL_PIXELFORMAT_RGBA8888, (int) SDL.SDL_TextureAccess.SDL_TEXTUREACCESS_STATIC, size, size);
            targetSurface = SDL.SDL_CreateRGBSurfaceWithFormat(0, SpriteSize, SpriteSize, 0, SDL.SDL_PIXELFORMAT_RGBA8888);

            foreach (var sprite in (Sprite[])Enum.GetValues(typeof(Sprite)))
            {
                if (sprite != Sprite.None && sprite != Sprite.FirstCustom)
                {
                    var surface = SDL_image.IMG_Load("data/" + sprite + ".png");
                    UpdateSprite(sprite, surface);
                    SDL.SDL_FreeSurface(surface);
                }
            }
        }

        public Sprite NewSprite() => nextId++;

        private SDL.SDL_Rect SpriteToRect(Sprite sprite)
        {
            var spriteId = (int) sprite;
            var row = spriteId >> SpritesPerRowLog;
            var column = spriteId & ColumnMask;
            if (row > SpritesPerDimension)
                throw new NotSupportedException("Sprite index is too large");
            return new SDL.SDL_Rect {x = row << SpriteSizeLog, y = column << SpriteSizeLog, w = SpriteSize, h = SpriteSize};
        }

        public void UpdateSprite(Sprite sprite, IntPtr surface)
        {
            var rect = SpriteToRect(sprite);
            ref var surfaceData = ref RenderingUtils.AsSdlSurface(surface);
            if (surfaceData.w == SpriteSize && surfaceData.h == SpriteSize)
                SDL.SDL_UpdateTexture(handle, ref rect, surfaceData.pixels, surfaceData.pitch);
            else
            {
                var srcRect = new SDL.SDL_Rect {w = surfaceData.w, h = surfaceData.h};
                SDL.SDL_LowerBlitScaled(surface, ref srcRect, targetSurface, ref TargetSurfaceRect);
                ref var targetData = ref RenderingUtils.AsSdlSurface(targetSurface);
                SDL.SDL_UpdateTexture(handle, ref rect, targetData.pixels, targetData.pitch);
            }
        }
        
        protected override void ReleaseUnmanagedResources()
        {
            SDL.SDL_DestroyTexture(handle);
            SDL.SDL_FreeSurface(targetSurface);
        }
    }
}