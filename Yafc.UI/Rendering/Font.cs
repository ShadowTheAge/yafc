using System;
using System.Collections.Generic;
using SDL2;

namespace Yafc.UI;

public class Font(FontFile file, float size) {
    public static Font header { get; set; } = null!; // null-forgiving: Set by Main
    public static Font subheader { get; set; } = null!; // null-forgiving: Set by Main
    public static Font productionTableHeader { get; set; } = null!; // null-forgiving: Set by Main
    public static Font text { get; set; } = null!; // null-forgiving: Set by Main

    public readonly float size = size;
    private FontFile.FontSize? lastFontSize;

    public FontFile.FontSize GetFontSize(float pixelsPreUnit) {
        int actualSize = MathUtils.Round(pixelsPreUnit * size);

        if (lastFontSize == null || lastFontSize.size != actualSize) {
            lastFontSize = file.GetFontForSize(actualSize);
        }

        return lastFontSize;
    }

    public IntPtr GetHandle(float pixelsPreUnit) => GetFontSize(pixelsPreUnit).handle;

    public float GetLineSize(float pixelsPreUnit) => GetFontSize(pixelsPreUnit).lineSize / pixelsPreUnit;

    public void Dispose() => file.Dispose();
}

public class FontFile(string fileName) : IDisposable {
    public readonly string fileName = fileName;
    private readonly Dictionary<int, FontSize> sizes = [];

    public class FontSize : UnmanagedResource {
        public readonly int size;
        public readonly int lineSize;
        public FontSize(FontFile font, int size) {
            this.size = size;
            _handle = SDL_ttf.TTF_OpenFont(font.fileName, size);
            lineSize = SDL_ttf.TTF_FontLineSkip(_handle);
        }

        public IntPtr handle => _handle;
        protected override void ReleaseUnmanagedResources() => SDL_ttf.TTF_CloseFont(_handle);
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
