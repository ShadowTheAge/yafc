using System;
using System.Numerics;
using System.Runtime.InteropServices;
using SDL2;
using Serilog;

namespace Yafc.UI;

// Main window is resizable and hardware-accelerated unless forced to render via software by caller
public abstract class WindowMain : Window {
    protected void Create(string title, int display, float initialWidth, float initialHeight, bool maximized, bool forceSoftwareRenderer) {
        if (visible) {
            return;
        }

        pixelsPerUnit = CalculateUnitsToPixels(display);
        // Min width/height define the minimum size of the main window when it gets resized.
        // The minimal size prevents issues/unreachable spots within the UI (like dialogs that do not size with the window size).
        int minWidth = MathUtils.Round(85f * pixelsPerUnit);
        int minHeight = MathUtils.Round(60f * pixelsPerUnit);
        // Initial width/height define the initial size of the MainWindow when it is opened.
        int initialWidthPixels = Math.Max(minWidth, MathUtils.Round(initialWidth * pixelsPerUnit));
        int initialHeightPixels = Math.Max(minHeight, MathUtils.Round(initialHeight * pixelsPerUnit));
        SDL.SDL_WindowFlags flags = SDL.SDL_WindowFlags.SDL_WINDOW_RESIZABLE | (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? 0 : SDL.SDL_WindowFlags.SDL_WINDOW_OPENGL);

        if (maximized) {
            flags |= SDL.SDL_WindowFlags.SDL_WINDOW_MAXIMIZED;
        }

        window = SDL.SDL_CreateWindow(title,
            SDL.SDL_WINDOWPOS_CENTERED_DISPLAY(display),
            SDL.SDL_WINDOWPOS_CENTERED_DISPLAY(display),
            initialWidthPixels, initialHeightPixels, flags
        );
        SDL.SDL_SetWindowMinimumSize(window, minWidth, minHeight);
        WindowResize();
        surface = new MainWindowDrawingSurface(this, forceSoftwareRenderer);
        base.Create();
    }

    protected override void BuildContents(ImGui gui) {
        BuildContent(gui);
        gui.SetContextRect(new Rect(default, size));
    }

    protected abstract void BuildContent(ImGui gui);

    protected override void OnRepaint() {
        rootGui.Rebuild();
        base.OnRepaint();
    }

    protected internal override void WindowResize() {
        SDL.SDL_GetWindowSize(window, out int windowWidth, out int windowHeight);
        contentSize = new Vector2(windowWidth / pixelsPerUnit, windowHeight / pixelsPerUnit);
        base.WindowResize();
    }

    protected bool IsMaximized {
        get {
            SDL.SDL_WindowFlags flags = (SDL.SDL_WindowFlags)SDL.SDL_GetWindowFlags(window);
            return flags.HasFlag(SDL.SDL_WindowFlags.SDL_WINDOW_MAXIMIZED);
        }
    }

    protected WindowMain(Padding padding) : base(padding) { }
}

internal class MainWindowDrawingSurface : DrawingSurface {
    private static readonly ILogger logger = Logging.GetLogger<MainWindowDrawingSurface>();
    private readonly IconAtlas atlas = new IconAtlas();
    private readonly IntPtr circleTexture;

    public override Window window { get; }

    /// <summary>
    /// Function <c>PickRenderDriver()</c> picks the best rendering backend available on the platform.
    ///
    /// This seems like something that SDL2 should do on its own, since the point of SDL2 is to abstract across platform render
    /// APIs, and it sort of does - but SDL2 has been around for a really long time and its defaults reflect that. On Windows,
    /// it will autoselect DirectX/Direct3D 9 if given half a chance; DX9 was of course the version that shipped in 2002 and
    /// supported Windows 98. This is despite the fact that SDL2 supports DX12 and DX11 where possible, and in 2024 "where possible"
    /// is really going to be "everywhere" - it just doesn't seem to default select them.
    ///
    /// Instead, we can specify the render driver to use when building a renderer instance; to figure out which one, you have to
    /// go iterate through all the render drivers the library supports and pick the right one by string comparing its name.
    /// </summary>
    /// <param name="flags">
    /// The flags you were going to/are about to pass to SDL_CreateRenderer, just to make sure the function doesn't pick something
    /// incompatible (this is paranoia since the major renderers tend to support everything relevant).
    /// </param>
    /// <param name="forceSoftwareRenderer">
    /// If set, always return the appropriate index for the software renderer. This can be useful if your graphics hardware doesn't support
    /// the rendering API that would otherwise be returned.
    /// </param>
    /// <returns>The index of the selected render driver, including 0 (SDL autoselect) if no known-best driver exists on this machine.
    /// This value should be fed to the second argument of SDL_CreateRenderer()</returns>
    private int PickRenderDriver(SDL.SDL_RendererFlags flags, bool forceSoftwareRenderer) {
        nint numRenderDrivers = SDL.SDL_GetNumRenderDrivers();
        logger.Debug($"Render drivers available: {numRenderDrivers}");
        int selectedRenderDriver = 0;

        for (int thisRenderDriver = 0; thisRenderDriver < numRenderDrivers; thisRenderDriver++) {
            nint res = SDL.SDL_GetRenderDriverInfo(thisRenderDriver, out SDL.SDL_RendererInfo rendererInfo);

            if (res != 0) {
                string reason = SDL.SDL_GetError();
                logger.Warning($"Render driver {thisRenderDriver} GetInfo failed: {res}: {reason}");
                continue;
            }

            // This is for some reason the one data structure that the dotnet library doesn't provide a native unmarshal for
            string? driverName = Marshal.PtrToStringAnsi(rendererInfo.name);

            if (driverName is null) {
                logger.Warning($"Render driver {thisRenderDriver} has an empty name, cannot compare, skipping");
                continue;
            }

            logger.Debug($"Render driver {thisRenderDriver} is {driverName} flags 0x{rendererInfo.flags:X}");

            // SDL2 does actually have a fixed (from code) ordering of available render drivers, so doing a full list scan instead of returning
            // immediately is a bit paranoid, but paranoia comes well-recommended when dealing with graphics drivers
            if (forceSoftwareRenderer) {
                if (driverName == "software") {
                    logger.Debug($"Selecting render driver {thisRenderDriver} (software) because it was forced");
                    selectedRenderDriver = thisRenderDriver;
                }
            }
            else {
                if ((rendererInfo.flags | (uint)flags) != rendererInfo.flags) {
                    logger.Debug($"Render driver {driverName} flags do not cover requested flags {flags}, skipping");

                    continue;
                }

                if (driverName == "direct3d12") {
                    logger.Debug($"Selecting render driver {thisRenderDriver} (DX12)");
                    selectedRenderDriver = thisRenderDriver;
                }
                else if (driverName == "direct3d11" && selectedRenderDriver == 0) {
                    logger.Debug($"Selecting render driver {thisRenderDriver} (DX11)");
                    selectedRenderDriver = thisRenderDriver;
                }
            }
        }

        logger.Debug($"Selected render driver index {selectedRenderDriver}");

        return selectedRenderDriver;
    }

    public MainWindowDrawingSurface(WindowMain window, bool forceSoftwareRenderer) : base(window.pixelsPerUnit) {
        this.window = window;

        renderer = SDL.SDL_CreateRenderer(window.window, PickRenderDriver(SDL.SDL_RendererFlags.SDL_RENDERER_PRESENTVSYNC, forceSoftwareRenderer), SDL.SDL_RendererFlags.SDL_RENDERER_PRESENTVSYNC);
        _ = SDL.SDL_GetRendererInfo(renderer, out SDL.SDL_RendererInfo info);
        logger.Information($"Driver: {SDL.SDL_GetCurrentVideoDriver()} Renderer: {Marshal.PtrToStringAnsi(info.name)}");
        circleTexture = SDL.SDL_CreateTextureFromSurface(renderer, RenderingUtils.CircleSurface);
        byte colorMod = RenderingUtils.darkMode ? (byte)255 : (byte)0;
        _ = SDL.SDL_SetTextureColorMod(circleTexture, colorMod, colorMod, colorMod);
    }

    internal override void DrawIcon(SDL.SDL_Rect position, Icon icon, SchemeColor color) => atlas.DrawIcon(renderer, icon, position, color.ToSdlColor());

    internal override void DrawBorder(SDL.SDL_Rect position, RectangleBorder border) {
        RenderingUtils.GetBorderParameters(pixelsPerUnit, border, out int top, out int side, out int bottom);
        RenderingUtils.GetBorderBatch(position, top, side, bottom, ref blitMapping);
        var bm = blitMapping;

        for (int i = 0; i < bm.Length; i++) {
            ref var cur = ref bm[i];
            _ = SDL.SDL_RenderCopy(renderer, circleTexture, ref cur.texture, ref cur.position);
        }
    }
}
