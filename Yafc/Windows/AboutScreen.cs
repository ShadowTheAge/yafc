using Yafc.UI;

namespace Yafc;
public class AboutScreen : WindowUtility {
    public const string Github = "https://github.com/have-fun-was-taken/yafc-ce";

    public AboutScreen(Window parent) : base(ImGuiUtils.DefaultScreenPadding) => Create("About YAFC-CE", 50, parent);

    protected override void BuildContents(ImGui gui) {
        gui.allocator = RectAllocator.Center;
        gui.BuildText("Yet Another Factorio Calculator", new TextBlockDisplayStyle(Font.header, Alignment: RectAlignment.Middle));
        gui.BuildText("(Community Edition)", TextBlockDisplayStyle.Centered);
        gui.BuildText("Copyright 2020-2021 ShadowTheAge", TextBlockDisplayStyle.Centered);
        gui.BuildText("Copyright 2024 YAFC Community", TextBlockDisplayStyle.Centered);
        gui.allocator = RectAllocator.LeftAlign;
        gui.AllocateSpacing(1.5f);
        gui.BuildText("This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.", TextBlockDisplayStyle.WrappedText);
        gui.BuildText("This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.", TextBlockDisplayStyle.WrappedText);
        using (gui.EnterRow(0.3f)) {
            gui.BuildText("Full license text:");
            BuildLink(gui, "https://gnu.org/licenses/gpl-3.0.html");
        }
        using (gui.EnterRow(0.3f)) {
            gui.BuildText("Github YAFC-CE page and documentation:");
            BuildLink(gui, Github);
        }
        gui.AllocateSpacing(1.5f);
        gui.BuildText("Free and open-source third-party libraries used:", Font.subheader);
        BuildLink(gui, "https://dotnet.microsoft.com/", "Microsoft .NET core and libraries");
        using (gui.EnterRow(0.3f)) {
            BuildLink(gui, "https://libsdl.org/index.php", "Simple DirectMedia Layer 2.0");
            gui.BuildText("and");
            BuildLink(gui, "https://github.com/flibitijibibo/SDL2-CS", "SDL2-CS");
        }
        using (gui.EnterRow(0.3f)) {
            gui.BuildText("Libraries for SDL2:");
            BuildLink(gui, "http://libpng.org/pub/png/libpng.html", "libpng,");
            BuildLink(gui, "http://libjpeg.sourceforge.net/", "libjpeg,");
            BuildLink(gui, "https://freetype.org", "libfreetype");
            gui.BuildText("and");
            BuildLink(gui, "https://zlib.net/", "zlib");
        }
        using (gui.EnterRow(0.3f)) {
            gui.BuildText("Google");
            BuildLink(gui, "https://developers.google.com/optimization", "OR-Tools,");
            BuildLink(gui, "https://fonts.google.com/specimen/Roboto", "Roboto font family");
            gui.BuildText("and");
            BuildLink(gui, "https://material.io/resources/icons", "Material Design Icon collection");
        }

        using (gui.EnterRow(0.3f)) {
            BuildLink(gui, "https://lua.org/", "Lua 5.2");
            gui.BuildText("plus");
            BuildLink(gui, "https://github.com/pkulchenko/serpent", "Serpent library");
            gui.BuildText("and small bits from");
            BuildLink(gui, "https://github.com/NLua", "NLua");
        }

        using (gui.EnterRow(0.3f)) {
            BuildLink(gui, "https://wiki.factorio.com/", "Documentation on Factorio Wiki");
            gui.BuildText("and");
            BuildLink(gui, "https://lua-api.factorio.com/latest/", "Factorio API reference");
        }

        gui.AllocateSpacing(1.5f);
        gui.allocator = RectAllocator.Center;
        gui.BuildText("Factorio name, content and materials are trademarks and copyrights of Wube Software");
        BuildLink(gui, "https://factorio.com/");
    }

    private void BuildLink(ImGui gui, string url, string? text = null) {
        if (gui.BuildLink(text ?? url)) {
            Ui.VisitLink(url);
        }
    }
}
