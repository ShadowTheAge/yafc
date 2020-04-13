using System;
using System.Diagnostics;
using System.Drawing;
using SDL2;
using UI;

namespace FactorioCalc
{
    public class AboutScreen : Window
    {
        private readonly IWidget[] widgets;
        public AboutScreen()
        {
            var and = new FontString(Font.text, "and");
            
            widgets = new IWidget[]
            {
                new FontString(Font.header, "Yet Another Factorio Calculator", align: Alignment.Center),
                new FontString(Font.text, "Copyright 2020 ShadowTheAge", align:Alignment.Center),
                null,
                new FontString(Font.text, "This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.", wrap:true),
                new FontString(Font.text, "This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.", wrap:true),
                new Horizontal(new FontString(Font.text, "Full license text:"), new LinkText("https://gnu.org/licenses/")),
                new Horizontal(new FontString(Font.text, "Github project page: FILL ME"), new LinkText("https://gnu.org/licenses/")),
                null,
                new FontString(Font.subheader, "Free and open-source third-party libraries used:"),
                new LinkText("Microsoft .NET core and libraries", "https://dotnet.microsoft.com/"),
                new Horizontal(new LinkText("Simple DirectMedia Layer 2.0", "https://libsdl.org/index.php"),
                    and, new LinkText("SDL2-CS", "https://github.com/flibitijibibo/SDL2-CS")),
                new Horizontal(new FontString(Font.text, "Libraries for SDL2:"),
                    new LinkText("libpng,", "http://libpng.org/pub/png/libpng.html"), 
                    new LinkText("libjpeg,", "http://libjpeg.sourceforge.net/"),
                    new LinkText("libfreetype", "https://freetype.org"),
                    and, new LinkText("zlib", "https://zlib.net/")),
                new Horizontal(new FontString(Font.text, "Google"),
                    new LinkText("OR-Tools,", "https://developers.google.com/optimization"),
                    new LinkText("Roboto font family", "https://fonts.google.com/specimen/Roboto"),
                    and, new LinkText("Material Design Icon collection", "https://material.io/resources/icons")),
                new Horizontal(new LinkText("Lua 5.3", "https://lua.org/"),
                    new FontString(Font.text, "and bindings:"),
                    new LinkText("NLua", "https://github.com/NLua/NLua"),
                    and, new LinkText("KeraLua", "https://github.com/NLua/KeraLua")),
                new Horizontal(new LinkText("Documentation on Factorio Wiki", "https://wiki.factorio.com/"), and, new LinkText("Factorio API reference", "https://lua-api.factorio.com/latest/")),
                null,
                new FontString(Font.text, "Factorio name, content and materials are trademarks and copyrights of Wube Software", wrap:true, Alignment.Center),
                new LinkText("https://factorio.com/", align:Alignment.Center),
            };
            Create("About YAFC", 50, true);
        }
        
        protected override LayoutPosition BuildContent(RenderBatch batch, LayoutPosition location)
        {
            foreach (var widget in widgets)
            {
                if (widget != null)
                    location.Build(widget, batch, 0.5f);
                else location.Space(1.5f);
            }
            return location;
        }

        private class Horizontal : IWidget
        {
            private readonly IWidget[] widgets;
            public Horizontal(params IWidget[] widgets)
            {
                this.widgets = widgets;
            }
            
            public LayoutPosition Build(RenderBatch batch, LayoutPosition location)
            {
                var maxy = location.y;
                foreach (var widget in widgets)
                {
                    var built = widget.Build(batch, location);
                    location.left = built.right + 0.25f;
                    maxy = MathF.Max(maxy, built.y);
                }

                location.y = maxy;
                return location;
            }
        }

        private class LinkText : FontString, IWidget, IMouseEnterHandle, IMouseClickHandle
        {
            private readonly string url;
            private bool hover;

            public LinkText(string text, string url = null, Alignment align = Alignment.Left) : base(Font.text, text, align:align)
            {
                color = SchemeColor.Link;
                this.url = url ?? text;
            }

            public new LayoutPosition Build(RenderBatch batch, LayoutPosition location)
            {
                var buildLoc = base.Build(batch, location);
                var rect = buildLoc.GetRect(location);
                batch.DrawRectangle(rect, SchemeColor.None, RectangleShadow.None, this);
                if (hover)
                    batch.DrawRectangle(new RectangleF(buildLoc.left, buildLoc.y-0.2f, buildLoc.width, 0.1f), SchemeColor.Link);
                return buildLoc;
            }

            public void MouseEnter(RenderBatch batch)
            {
                hover = true;
                SDL.SDL_SetCursor(RenderingUtils.cursorHand);
                batch.Rebuild();
            }

            public void MouseExit(RenderBatch batch)
            {
                hover = false;
                SDL.SDL_SetCursor(RenderingUtils.cursorArrow);
                batch.Rebuild();
            }

            public void MouseClickUpdateState(bool mouseOverAndDown, int button, RenderBatch batch) {}

            public void MouseClick(int button, RenderBatch batch)
            {
                Process.Start(new ProcessStartInfo(url) {UseShellExecute = true});
            }
        }
    }
}