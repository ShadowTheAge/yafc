using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using YAFC.UI;

namespace YAFC {
    public static class WindowsClipboard {
        [DllImport("user32.dll")] private static extern bool OpenClipboard(IntPtr handle);
        [DllImport("user32.dll")] private static extern bool EmptyClipboard();
        [DllImport("user32.dll")] private static extern IntPtr SetClipboardData(uint format, IntPtr data);
        [DllImport("user32.dll")] private static extern bool CloseClipboard();

        private static unsafe void CopyToClipboard<T>(uint format, in T header, Span<byte> data) where T : unmanaged {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return;
            int headersize = Unsafe.SizeOf<T>();
            var ptr = Marshal.AllocHGlobal(headersize + data.Length);
            _ = OpenClipboard(IntPtr.Zero);
            try {
                Marshal.StructureToPtr(header, ptr, false);
                Span<byte> targetSpan = new Span<byte>((void*)(ptr + headersize), data.Length);
                data.CopyTo(targetSpan);
                _ = EmptyClipboard();
                _ = SetClipboardData(format, ptr);
                ptr = IntPtr.Zero;
            }
            finally {
                Marshal.FreeHGlobal(ptr);
                _ = CloseClipboard();
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BitmapInfoHeader {
            public uint biSize;
            public int biWidth;
            public int biHeight;
            public short biPlanes;
            public short biBitCount;
            public uint biCompression;
            public uint biSizeImage;
            public int biXPlesPerMeter;
            public int biYPelsPerMeter;
            public uint biClrUsed;
            public uint biClrImportant;
        }

        public static unsafe void CopySurfaceToClipboard(MemoryDrawingSurface surface) {
            ref var surfaceinfo = ref RenderingUtils.AsSdlSurface(surface.surface);
            int width = surfaceinfo.w;
            int height = surfaceinfo.h;
            int pitch = surfaceinfo.pitch;
            int size = pitch * surfaceinfo.h;

            // Windows expect images starting at bottom
            Span<byte> flippedPixels = new Span<byte>(new byte[size]);
            Span<byte> originalPixels = new Span<byte>((void*)surfaceinfo.pixels, size);
            for (int i = 0; i < surfaceinfo.h; i++)
                originalPixels.Slice(i * pitch, pitch).CopyTo(flippedPixels.Slice((height - i - 1) * pitch, pitch));

            BitmapInfoHeader header = new BitmapInfoHeader {
                biSize = (uint)Unsafe.SizeOf<BitmapInfoHeader>(),
                biWidth = width,
                biHeight = height,
                biPlanes = 1,
                biBitCount = 32,
                biCompression = 0,
                biSizeImage = (uint)size,
            };
            CopyToClipboard(8, header, flippedPixels);
        }

    }
}
