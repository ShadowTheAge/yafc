using System;
using SDL2;

namespace YAFC.UI {
    public class IconAtlas {
        private IntPtr prevRender;

        private const int IconSize = IconCollection.IconSize;
        private const int TextureSize = 2048;
        private const int IconStride = IconSize + 1;
        private const int IconsPerRow = TextureSize / IconStride;
        private const int IconPerTexture = IconsPerRow * IconsPerRow;

        private struct TextureInfo {
            public TextureInfo(IntPtr texture) {
                this.texture = texture;
                existMap = new bool[IconPerTexture];
                color = RenderingUtils.White;
            }

            public readonly IntPtr texture;
            public readonly bool[] existMap;
            public SDL.SDL_Color color;
        }

        private TextureInfo[] textures = new TextureInfo[1];

        public void DrawIcon(IntPtr renderer, Icon icon, SDL.SDL_Rect position, SDL.SDL_Color color) {
            if (renderer != prevRender) {
                Array.Clear(textures, 0, textures.Length);
            }
            prevRender = renderer;
            var index = (int)icon;
            ref var texture = ref textures[0];
            var ix = index % IconsPerRow;
            var iy = index / IconsPerRow;
            if (index >= IconPerTexture) // That is very unlikely
            {
                var texId = index / IconPerTexture;
                if (texId >= textures.Length)
                    Array.Resize(ref textures, texId + 1);
                index -= texId * IconPerTexture;
                iy -= texId * IconsPerRow;
                texture = ref textures[texId];
            }
            var rect = new SDL.SDL_Rect { x = ix * IconStride, y = iy * IconStride, w = IconSize, h = IconSize };
            if (texture.texture == IntPtr.Zero) {
                texture = new TextureInfo(SDL.SDL_CreateTexture(renderer, SDL.SDL_PIXELFORMAT_RGBA8888, (int)SDL.SDL_TextureAccess.SDL_TEXTUREACCESS_STATIC, TextureSize, TextureSize));
                SDL.SDL_SetTextureBlendMode(texture.texture, SDL.SDL_BlendMode.SDL_BLENDMODE_BLEND);
            }
            if (!texture.existMap[index]) {
                var iconSurfacePtr = IconCollection.GetIconSurface(icon);
                if (iconSurfacePtr == IntPtr.Zero) {
                    Console.Error.WriteLine("Non-existing icon: " + icon);
                    return;
                }
                ref var iconSurface = ref RenderingUtils.AsSdlSurface(iconSurfacePtr);
                texture.existMap[index] = true;
                SDL.SDL_UpdateTexture(texture.texture, ref rect, iconSurface.pixels, iconSurface.pitch);
            }

            if (texture.color.r != color.r || texture.color.g != color.g || texture.color.b != color.b)
                SDL.SDL_SetTextureColorMod(texture.texture, color.r, color.g, color.b);
            if (texture.color.a != color.a)
                SDL.SDL_SetTextureAlphaMod(texture.texture, color.a);

            texture.color = color;
            SDL.SDL_RenderCopy(renderer, texture.texture, ref rect, ref position);
        }
    }
}