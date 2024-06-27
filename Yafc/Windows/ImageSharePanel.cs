using System.IO;
using System.Runtime.InteropServices;
using SDL2;
using Yafc.UI;

namespace Yafc {
    public class ImageSharePanel : PseudoScreen {
        private readonly MemoryDrawingSurface surface;
        private readonly string header;
        private readonly string name;
        private static readonly string TempImageFile = Path.Combine(Path.GetTempPath(), "yafc_temp.png");
        private FilesystemScreen? fsScreen;
        private bool copied;

        public ImageSharePanel(MemoryDrawingSurface surface, string name) {
            copied = false;
            this.surface = surface;
            this.name = name;
            ref var surfaceData = ref RenderingUtils.AsSdlSurface(surface.surface);
            header = name + " (" + surfaceData.w + "x" + surfaceData.h + ")";

            _ = MainScreen.Instance.ShowPseudoScreen(this);
        }

        public override void Build(ImGui gui) {
            BuildHeader(gui, "Image generated");
            gui.BuildText(header, wrap: true);
            if (gui.BuildButton("Save as PNG")) {
                SaveAsPng();
            }

            if (gui.BuildButton("Save to temp folder and open")) {
                surface.SavePng(TempImageFile);
                Ui.VisitLink("file:///" + TempImageFile);
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && gui.BuildButton(copied ? "Copied to clipboard" : "Copy to clipboard (Ctrl+" + ImGuiUtils.ScanToString(SDL.SDL_Scancode.SDL_SCANCODE_C) + ")", active: !copied)) {
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
            return base.KeyUp(key);
        }

        private async void SaveAsPng() {
            fsScreen ??= new FilesystemScreen(header, "Save as PNG", "Save", null, FilesystemScreen.Mode.SelectOrCreateFile, name + ".png", MainScreen.Instance, null, "png");
            string? path = await fsScreen;
            if (path != null) {
                surface?.SavePng(path);
            }
        }

        protected override void Close(bool save = true) {
            base.Close(save);
            surface?.Dispose();
            fsScreen?.Close();
            fsScreen = null;
        }
    }
}
