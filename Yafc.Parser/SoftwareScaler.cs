using System;
using System.Runtime.CompilerServices;
using SDL2;
using Yafc.UI;

namespace Yafc.Parser;
internal static class SoftwareScaler {
    public static unsafe IntPtr DownscaleIcon(IntPtr surface, int targetSize) {
        ref var surfaceData = ref RenderingUtils.AsSdlSurface(surface);
        uint format = Unsafe.AsRef<SDL.SDL_PixelFormat>((void*)surfaceData.format).format;

        nint targetSurface = SDL.SDL_CreateRGBSurfaceWithFormat(0, targetSize, targetSize, 0, format);
        ref var targetSurfaceData = ref RenderingUtils.AsSdlSurface(targetSurface);
        int sourceSize = Math.Min(surfaceData.h, surfaceData.w);

        int pitch = surfaceData.pitch;
        int bpp = Math.Min(pitch / surfaceData.w, targetSurfaceData.pitch / targetSurfaceData.w);
        int fromY = 0;
        int* buf = stackalloc int[bpp];

        for (int y = 0; y < targetSize; y++) {
            int toY = (y + 1) * sourceSize / targetSize;
            int fromX = 0;

            for (int x = 0; x < targetSize; x++) {
                int toX = (x + 1) * sourceSize / targetSize;
                int c = 0;

                for (int sy = fromY; sy < toY; sy++) {
                    byte* pixels = (byte*)(surfaceData.pixels + (sy * pitch) + (fromX * bpp));

                    for (int sx = fromX; sx < toX; sx++) {
                        ++c;

                        for (int p = 0; p < bpp; p++) {
                            buf[p] += *pixels;
                            pixels++;
                        }
                    }
                }

                byte* targetPixels = (byte*)(targetSurfaceData.pixels + (y * targetSurfaceData.pitch) + (x * bpp));

                for (int p = 0; p < bpp; p++) {
                    int sum = buf[p];
                    *targetPixels = (byte)MathUtils.Clamp((float)sum / c, 0, 255);
                    targetPixels++;
                    buf[p] = 0;
                }

                fromX = toX;
            }

            fromY = toY;
        }

        SDL.SDL_FreeSurface(surface);

        return targetSurface;
    }
}
