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
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                return;
            }

            int headerSize = Unsafe.SizeOf<T>();
            var ptr = Marshal.AllocHGlobal(headerSize + data.Length);
            _ = OpenClipboard(IntPtr.Zero);
            try {
                Marshal.StructureToPtr(header, ptr, false);
                Span<byte> targetSpan = new Span<byte>((void*)(ptr + headerSize), data.Length);
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
            public int biXPixelPerMeter;
            public int biYPixelPerMeter;
            public uint biClrUsed;
            public uint biClrImportant;
        }

        public static unsafe void CopySurfaceToClipboard(MemoryDrawingSurface surface) {
            ref var surfaceInfo = ref RenderingUtils.AsSdlSurface(surface.surface);
            int width = surfaceInfo.w;
            int height = surfaceInfo.h;
            int pitch = surfaceInfo.pitch;
            int size = pitch * surfaceInfo.h;

            // Windows expect images starting at bottom
            Span<byte> flippedPixels = new Span<byte>(new byte[size]);
            Span<byte> originalPixels = new Span<byte>((void*)surfaceInfo.pixels, size);
            for (int i = 0; i < surfaceInfo.h; i++) {
                originalPixels.Slice(i * pitch, pitch).CopyTo(flippedPixels.Slice((height - i - 1) * pitch, pitch));
            }

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
