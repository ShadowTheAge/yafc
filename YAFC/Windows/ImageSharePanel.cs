using System.IO;
using System.Runtime.InteropServices;
using SDL2;
using YAFC.UI;

namespace YAFC {
    public class ImageSharePanel : PseudoScreen {
        private static readonly ImageSharePanel Instance = new ImageSharePanel();
        private MemoryDrawingSurface surface;
        private string header;
        private string name;
        private static readonly string TempImageFile = Path.Combine(Path.GetTempPath(), "yafc_temp.png");
        private FilesystemScreen fsscreen;
        private bool copied;

        public static void Show(MemoryDrawingSurface surface, string name) {
            Instance.SetSurface(surface, name);
            _ = MainScreen.Instance.ShowPseudoScreen(Instance);
        }

        public void SetSurface(MemoryDrawingSurface surface, string name) {
            copied = false;
            this.surface = surface;
            this.name = name;
            ref var surfaceData = ref RenderingUtils.AsSdlSurface(surface.surface);
            header = name + " (" + surfaceData.w + "x" + surfaceData.h + ")";
        }

        public override void Build(ImGui gui) {
            BuildHeader(gui, "Image generated");
            gui.BuildText(header, wrap: true);
            if (gui.BuildButton("Save as PNG"))
                SaveAsPng();
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
            fsscreen ??= new FilesystemScreen(header, "Save as PNG", "Save", null, FilesystemScreen.Mode.SelectOrCreateFile, name + ".png", MainScreen.Instance, null, "png");
            var path = await fsscreen;
            if (path != null)
                surface?.SavePng(path);
        }

        protected override void Close(bool save = true) {
            base.Close(save);
            if (surface != null) {
                surface.Dispose();
                surface = null;
            }

            fsscreen?.Close();
            fsscreen = null;
        }
    }
}
