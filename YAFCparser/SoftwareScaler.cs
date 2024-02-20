using System;
using System.Runtime.CompilerServices;
using SDL2;
using YAFC.UI;

namespace YAFC.Parser {
    internal static class SoftwareScaler {
        public static unsafe IntPtr DownscaleIcon(IntPtr surface, int targetSize) {
            ref var surfaceData = ref RenderingUtils.AsSdlSurface(surface);
            var format = Unsafe.AsRef<SDL.SDL_PixelFormat>((void*)surfaceData.format).format;

            var targetSurface = SDL.SDL_CreateRGBSurfaceWithFormat(0, targetSize, targetSize, 0, format);
            ref var targetSurfaceData = ref RenderingUtils.AsSdlSurface(targetSurface);
            var sourceSize = Math.Min(surfaceData.h, surfaceData.w);

            var pitch = surfaceData.pitch;
            var bpp = Math.Min(pitch / surfaceData.w, targetSurfaceData.pitch / targetSurfaceData.w);
            var fromY = 0;
            var buf = stackalloc int[bpp];
            for (var y = 0; y < targetSize; y++) {
                var toY = (y + 1) * sourceSize / targetSize;
                var fromX = 0;
                for (var x = 0; x < targetSize; x++) {
                    var toX = (x + 1) * sourceSize / targetSize;
                    var c = 0;
                    for (var sy = fromY; sy < toY; sy++) {
                        var pixels = (byte*)(surfaceData.pixels + sy * pitch + fromX * bpp);
                        for (var sx = fromX; sx < toX; sx++) {
                            ++c;
                            for (var p = 0; p < bpp; p++) {
                                buf[p] += *pixels;
                                pixels++;
                            }
                        }
                    }

                    var targetPixels = (byte*)(targetSurfaceData.pixels + y * targetSurfaceData.pitch + x * bpp);
                    for (var p = 0; p < bpp; p++) {
                        var sum = buf[p];
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
}