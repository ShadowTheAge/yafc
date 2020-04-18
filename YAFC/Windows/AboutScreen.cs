using System;
using System.Diagnostics;
using System.Drawing;
using SDL2;
using YAFC.UI;

namespace YAFC
{
    public class AboutScreen : Window
    {
        private readonly object[] widgets;
        public AboutScreen(Window parent)
        {            
            widgets = new object[]
            {
                RectAllocator.Center,
                new FontString(Font.header, "Yet Another Factorio Calculator"),
                new FontString(Font.text, "Copyright 2020 ShadowTheAge"),
                RectAllocator.LeftAlign,
                null,
                new FontString(Font.text, "This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.", wrap:true),
                new FontString(Font.text, "This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.", wrap:true),
                new []{new FontString(Font.text, "Full license text:"), new LinkText("https://gnu.org/licenses/")},
                new []{new FontString(Font.text, "Github project page: FILL ME"), new LinkText("https://gnu.org/licenses/")},
                null,
                new FontString(Font.subheader, "Free and open-source third-party libraries used:"),
                new LinkText("Microsoft .NET core and libraries", "https://dotnet.microsoft.com/"),
                new []{new LinkText("Simple DirectMedia Layer 2.0", "https://libsdl.org/index.php"),
                    new FontString(Font.text, "and"),
                    new LinkText("SDL2-CS", "https://github.com/flibitijibibo/SDL2-CS")},
                new []{new FontString(Font.text, "Libraries for SDL2:"),
                    new LinkText("libpng,", "http://libpng.org/pub/png/libpng.html"), 
                    new LinkText("libjpeg,", "http://libjpeg.sourceforge.net/"),
                    new LinkText("libfreetype", "https://freetype.org"),
                    new FontString(Font.text, "and"),
                    new LinkText("zlib", "https://zlib.net/")},
                new []{new FontString(Font.text, "Google"),
                    new LinkText("OR-Tools,", "https://developers.google.com/optimization"),
                    new LinkText("Roboto font family", "https://fonts.google.com/specimen/Roboto"),
                    new FontString(Font.text, "and"),
                    new LinkText("Material Design Icon collection", "https://material.io/resources/icons")},
                new []{new LinkText("Lua 5.3", "https://lua.org/"),
                    new FontString(Font.text, "and bindings:"),
                    new LinkText("NLua", "https://github.com/NLua/NLua"),
                    new FontString(Font.text, "and"),
                    new LinkText("KeraLua", "https://github.com/NLua/KeraLua")},
                new []{new LinkText("Documentation on Factorio Wiki", "https://wiki.factorio.com/"),
                    new FontString(Font.text, "and"),
                    new LinkText("Factorio API reference", "https://lua-api.factorio.com/latest/")},
                null,
                RectAllocator.Center,
                new FontString(Font.text, "Factorio name, content and materials are trademarks and copyrights of Wube Software"),
                new LinkText("https://factorio.com/"),
            };
            Create("About YAFC", 50, true, parent);
        }
        
        protected override void BuildContent(LayoutState state)
        {
            foreach (var elem in widgets)
            {
                switch (elem)
                {
                    case null:
                        state.AllocateSpacing(1.5f);
                        break;
                    case IWidget widget:
                        state.Build(widget);
                        break;
                    case RectAllocator allocator:
                        state.allocator = allocator;
                        break;
                    case IWidget[] row:
                        using (state.EnterGroup(default, RectAllocator.LeftRow))
                        {
                            state.spacing = 0.25f;
                            foreach (var rowWidget in row)
                                state.Build(rowWidget);
                        }
                        state.AllocateSpacing();
                        break;
                }
            }
        }

        private class LinkText : FontString, IWidget, IMouseEnterHandle, IMouseClickHandle
        {
            private readonly string url;
            private bool hover;

            public LinkText(string text, string url = null) : base(Font.text, text)
            {
                color = SchemeColor.Link;
                this.url = url ?? text;
            }

            public new void Build(LayoutState state)
            {
                base.Build(state);
                var rect = state.lastRect;
                state.batch.DrawRectangle(rect, SchemeColor.None, RectangleBorder.None, this);
                if (hover)
                    state.batch.DrawRectangle(new RectangleF(rect.X, rect.Bottom-0.2f, rect.Width, 0.1f), SchemeColor.Link);
            }

            public void MouseEnter(UiBatch batch)
            {
                hover = true;
                SDL.SDL_SetCursor(RenderingUtils.cursorHand);
                batch.Rebuild();
            }

            public void MouseExit(UiBatch batch)
            {
                hover = false;
                SDL.SDL_SetCursor(RenderingUtils.cursorArrow);
                batch.Rebuild();
            }

            public void MouseClickUpdateState(bool mouseOverAndDown, int button, UiBatch batch) {}

            public void MouseClick(int button, UiBatch batch)
            {
                Process.Start(new ProcessStartInfo(url) {UseShellExecute = true});
            }
        }
    }
}