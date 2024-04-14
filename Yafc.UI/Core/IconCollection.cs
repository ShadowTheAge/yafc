using System;
using System.Collections.Generic;
using SDL2;

namespace Yafc.UI {
    public static class IconCollection {
        public const int IconSize = 32;
        public static SDL.SDL_Rect IconRect = new SDL.SDL_Rect { w = IconSize, h = IconSize };

        private static readonly List<IntPtr> icons = new List<IntPtr>();

        static IconCollection() {
            icons.Add(IntPtr.Zero);
            var iconId = Icon.None + 1;
            while (iconId != Icon.FirstCustom) {
                var surface = SDL_image.IMG_Load("Data/Icons/" + iconId + ".png");
                var surfaceRgba = SDL.SDL_CreateRGBSurfaceWithFormat(0, IconSize, IconSize, 0, SDL.SDL_PIXELFORMAT_RGBA8888);
                _ = SDL.SDL_FillRect(surfaceRgba, IntPtr.Zero, 0xFFFFFF00);
                _ = SDL.SDL_BlitSurface(surface, IntPtr.Zero, surfaceRgba, IntPtr.Zero);
                SDL.SDL_FreeSurface(surface);
                icons.Add(surfaceRgba);
                iconId++;
            }
        }

        public static int IconCount => icons.Count;

        public static Icon AddIcon(IntPtr surface) {
            Icon id = (Icon)icons.Count;
            ref var surfaceData = ref RenderingUtils.AsSdlSurface(surface);
            if (surfaceData.w == IconSize && surfaceData.h == IconSize) {
                icons.Add(surface);
            }
            else {
                var blit = SDL.SDL_CreateRGBSurfaceWithFormat(0, IconSize, IconSize, 0, SDL.SDL_PIXELFORMAT_RGBA8888);
                SDL.SDL_Rect srcRect = new SDL.SDL_Rect { w = surfaceData.w, h = surfaceData.h };
                _ = SDL.SDL_LowerBlitScaled(surface, ref srcRect, blit, ref IconRect);
                icons.Add(blit);
                SDL.SDL_FreeSurface(surface);
            }
            return id;
        }

        public static IntPtr GetIconSurface(Icon icon) {
            return icons[(int)icon];
        }

        public static void ClearCustomIcons() {
            int firstCustomIconId = (int)Icon.FirstCustom;
            for (int i = firstCustomIconId; i < icons.Count; i++) {
                SDL.SDL_FreeSurface(icons[i]);
            }

            icons.RemoveRange(firstCustomIconId, icons.Count - firstCustomIconId);
        }
    }
}
