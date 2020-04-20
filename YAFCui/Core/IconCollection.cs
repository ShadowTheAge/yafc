using System;
using System.Collections.Generic;
using SDL2;

namespace YAFC.UI
{
    public static class IconCollection
    {
        public const int IconSize = 32;
        public static SDL.SDL_Rect IconRect = new SDL.SDL_Rect {w = IconSize, h = IconSize}; 
        
        private static readonly List<IntPtr> icons = new List<IntPtr>();

        static IconCollection()
        {
            icons.Add(IntPtr.Zero);
            var iconId = Icon.None + 1;
            while (iconId != Icon.FirstCustom)
            {
                var surface = SDL_image.IMG_Load("Data/Icons/" + iconId + ".png");
                var surfaceRgba = SDL.SDL_CreateRGBSurfaceWithFormat(0, 32, 32, 0, SDL.SDL_PIXELFORMAT_RGBA8888);
                SDL.SDL_BlitSurface(surface, IntPtr.Zero, surfaceRgba, IntPtr.Zero);
                SDL.SDL_FreeSurface(surface);
                icons.Add(surfaceRgba);
                iconId++;
            }
        }

        public static int IconCount => icons.Count;

        public static Icon AddIcon(IntPtr surface)
        {
            var id = (Icon) icons.Count;
            ref var surfaceData = ref RenderingUtils.AsSdlSurface(surface);
            if (surfaceData.w == IconSize && surfaceData.h == IconSize)
                icons.Add(surface);
            else
            {
                var blit = SDL.SDL_CreateRGBSurfaceWithFormat(0, IconSize, IconSize, 0, SDL.SDL_PIXELFORMAT_RGBA8888);
                var srcRect = new SDL.SDL_Rect {w = surfaceData.w, h = surfaceData.h};
                SDL.SDL_LowerBlitScaled(surface, ref srcRect, blit, ref IconRect);
                icons.Add(blit);
                SDL.SDL_FreeSurface(surface);
            }
            return id;
        }

        public static IntPtr GetIconSurface(Icon icon) => icons[(int) icon];
    }
}