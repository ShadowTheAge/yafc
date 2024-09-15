using System.IO;
using System.Runtime.InteropServices;
using SDL2;
using Yafc.UI;

namespace Yafc;
public class ImageSharePanel : PseudoScreen {
    private readonly MemoryDrawingSurface surface;
    private readonly string header;
    private readonly string name;
    private static readonly string TempImageFile = Path.Combine(Path.GetTempPath(), "yafc_temp.png");
    private bool copied;

    public ImageSharePanel(MemoryDrawingSurface surface, string name) {
        copied = false;
        this.surface = surface;
        this.name = name;
        ref var surfaceData = ref RenderingUtils.AsSdlSurface(surface.surface);
        header = name + " (" + surfaceData.w + "x" + surfaceData.h + ")";
        cleanupCallback = surface.Dispose;

        _ = MainScreen.Instance.ShowPseudoScreen(this);
    }

    public override void Build(ImGui gui) {
        BuildHeader(gui, "Image generated");
        gui.BuildText(header, TextBlockDisplayStyle.WrappedText);
        if (gui.BuildButton("Save as PNG")) {
            SaveAsPng();
        }

        if (gui.BuildButton("Save to temp folder and open")) {
            surface.SavePng(TempImageFile);
            Ui.VisitLink("file:///" + TempImageFile);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            && gui.BuildButton(copied ? "Copied to clipboard" : "Copy to clipboard (Ctrl+" + ImGuiUtils.ScanToString(SDL.SDL_Scancode.SDL_SCANCODE_C) + ")", active: !copied)) {

            WindowsClipboard.CopySurfaceToClipboard(surface);
            copied = true;
        }
    }

    public override bool KeyDown(SDL.SDL_Keysym key) {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && key.scancode == SDL.SDL_Scancode.SDL_SCANCODE_C && InputSystem.Instance.control) {
            WindowsClipboard.CopySurfaceToClipboard(surface);
            copied = true;
            Rebuild();
        }
        return base.KeyDown(key);
    }

    private async void SaveAsPng() {
        string? path = await new FilesystemScreen(header, "Save as PNG", "Save", null, FilesystemScreen.Mode.SelectOrCreateFile, name + ".png", MainScreen.Instance, null, "png");
        if (path != null) {
            surface?.SavePng(path);
        }
    }
}
