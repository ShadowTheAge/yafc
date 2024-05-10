using System;
using System.Collections.Generic;
using SDL2;

namespace Yafc.UI {
    public class Font {
        public static Font header;
        public static Font subheader;
        public static Font productionTableHeader;
        public static Font text;

        public readonly float size;

        private readonly FontFile fontFile;
        private FontFile.FontSize lastFontSize;

        public FontFile.FontSize GetFontSize(float pixelsPreUnit) {
            int actualSize = MathUtils.Round(pixelsPreUnit * size);
            if (lastFontSize == null || lastFontSize.size != actualSize) {
                lastFontSize = fontFile.GetFontForSize(actualSize);
            }

            return lastFontSize;
        }

        public IntPtr GetHandle(float pixelsPreUnit) {
            return GetFontSize(pixelsPreUnit).handle;
        }

        public float GetLineSize(float pixelsPreUnit) {
            return GetFontSize(pixelsPreUnit).lineSize / pixelsPreUnit;
        }

        public Font(FontFile file, float size) {
            this.size = size;
            fontFile = file;
        }

        public void Dispose() {
            fontFile.Dispose();
        }
    }

    public class FontFile : IDisposable {
        public readonly string fileName;
        private readonly Dictionary<int, FontSize> sizes = new Dictionary<int, FontSize>();
        public FontFile(string fileName) {
            this.fileName = fileName;
        }

        public class FontSize : UnmanagedResource {
            public readonly int size;
            public readonly int lineSize;
            public FontSize(FontFile font, int size) {
                this.size = size;
                _handle = SDL_ttf.TTF_OpenFont(font.fileName, size);
                lineSize = SDL_ttf.TTF_FontLineSkip(_handle);
            }

            public IntPtr handle => _handle;
            protected override void ReleaseUnmanagedResources() {
                SDL_ttf.TTF_CloseFont(_handle);
            }
        }

        public FontSize GetFontForSize(int size) {
            if (sizes.TryGetValue(size, out var result)) {
                return result;
            }

            return sizes[size] = new FontSize(this, size);
        }

        public void Dispose() {
            foreach (var (_, size) in sizes) {
                size.Dispose();
            }

            sizes.Clear();
        }
    }
}
