using System;
using SDL2;

namespace UI
{
    internal sealed class SpriteAtlas : SdlResource
    {
        private Sprite nextId = Sprite.FirstCustom;
        private readonly IntPtr targetSurface;

        private const int TextureSize = 2048;
        private const int SpriteSize = 32;
        private const int SpriteStride = SpriteSize+1;
        private const int SpritesPerRow = TextureSize / SpriteStride;
        private static SDL.SDL_Rect TargetSurfaceRect = new SDL.SDL_Rect {w = SpriteSize, h = SpriteSize};
        public IntPtr handle => _handle;

        public SpriteAtlas()
        {
            _handle = SDL.SDL_CreateTexture(RenderingUtils.renderer, SDL.SDL_PIXELFORMAT_RGBA8888, (int) SDL.SDL_TextureAccess.SDL_TEXTUREACCESS_STATIC, TextureSize, TextureSize);
            targetSurface = SDL.SDL_CreateRGBSurfaceWithFormat(0, SpriteSize, SpriteSize, 0, SDL.SDL_PIXELFORMAT_RGBA8888);
            SDL.SDL_SetTextureBlendMode(_handle, SDL.SDL_BlendMode.SDL_BLENDMODE_BLEND);

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

        internal static SDL.SDL_Rect SpriteToRect(Sprite sprite)
        {
            var spriteId = (int) sprite;
            var row = spriteId / SpritesPerRow;
            var column = spriteId % SpritesPerRow;
            if (row > SpritesPerRow)
                throw new NotSupportedException("Sprite index is too large");
            return new SDL.SDL_Rect {x = row * SpriteStride, y = column * SpriteStride, w = SpriteSize, h = SpriteSize};
        }

        public void UpdateSprite(Sprite sprite, IntPtr surface)
        {
            var rect = SpriteToRect(sprite);
            ref var surfaceData = ref RenderingUtils.AsSdlSurface(surface);
            if (surfaceData.w == SpriteSize && surfaceData.h == SpriteSize)
                SDL.SDL_UpdateTexture(_handle, ref rect, surfaceData.pixels, surfaceData.pitch);
            else
            {
                var srcRect = new SDL.SDL_Rect {w = surfaceData.w, h = surfaceData.h};
                SDL.SDL_LowerBlitScaled(surface, ref srcRect, targetSurface, ref TargetSurfaceRect);
                ref var targetData = ref RenderingUtils.AsSdlSurface(targetSurface);
                SDL.SDL_UpdateTexture(_handle, ref rect, targetData.pixels, targetData.pitch);
            }
        }
        
        protected override void ReleaseUnmanagedResources()
        {
            SDL.SDL_DestroyTexture(_handle);
            SDL.SDL_FreeSurface(targetSurface);
        }
    }
}