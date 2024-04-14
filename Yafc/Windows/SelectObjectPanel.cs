using System;
using System.Collections.Generic;
using System.Numerics;
using SDL2;
using Yafc.Model;
using Yafc.UI;

namespace Yafc {
    public abstract class SelectObjectPanel<T> : PseudoScreen<T> {
        protected readonly SearchableList<FactorioObject> list;
        protected string header;
        protected Rect searchBox;
        protected bool extendHeader;

        protected SelectObjectPanel() : base(40f) {
            list = new SearchableList<FactorioObject>(30, new Vector2(2.5f, 2.5f), ElementDrawer, ElementFilter);
        }

        protected void Select<U>(IEnumerable<U> list, string header, Action<U> select, IComparer<U> ordering, Action<T, Action<FactorioObject>> process, bool allowNone) where U : FactorioObject {
            _ = MainScreen.Instance.ShowPseudoScreen(this);
            extendHeader = typeof(U) == typeof(FactorioObject);
            List<U> data = new List<U>(list);
            data.Sort(ordering);
            if (allowNone) {
                data.Insert(0, null);
            }

            this.list.filter = default;
            this.list.data = data;
            this.header = header;
            Rebuild();
            complete = (selected, x) => process(x, x => {
                if (x is U u) {
                    if (ordering is DataUtils.FavoritesComparer<U> favoritesComparer) {
                        favoritesComparer.AddToFavorite(u);
                    }

                    select(u);
                }
                else if (allowNone && selected) {
                    select(null);
                }
            });
        }

        protected void Select<U>(IEnumerable<U> list, string header, Action<U> select, Action<T, Action<FactorioObject>> process, bool allowNone = false) where U : FactorioObject {
            Select(list, header, select, DataUtils.DefaultOrdering, process, allowNone);
        }

        private void ElementDrawer(ImGui gui, FactorioObject element, int index) {
            if (element == null) {
                if (gui.BuildRedButton(Icon.Close)) {
                    CloseWithResult(default);
                }
            }
            else {
                NonNullElementDrawer(gui, element, index);
            }
        }

        protected abstract void NonNullElementDrawer(ImGui gui, FactorioObject element, int index);

        private bool ElementFilter(FactorioObject data, SearchQuery query) {
            return data.Match(query);
        }

        public override void Build(ImGui gui) {
            BuildHeader(gui, header);
            if (gui.BuildSearchBox(list.filter, out var newFilter, "Start typing for search")) {
                list.filter = newFilter;
            }

            searchBox = gui.lastRect;
            list.Build(gui);
        }

        public override bool KeyDown(SDL.SDL_Keysym key) {
            contents.SetTextInputFocus(searchBox, list.filter.query);
            return base.KeyDown(key);
        }
    }
}
