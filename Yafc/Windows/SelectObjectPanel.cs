using System;
using System.Collections.Generic;
using System.Numerics;
using SDL2;
using Yafc.Model;
using Yafc.UI;

namespace Yafc {
    /// <summary>
    /// Represents a panel that can generate a result by selecting zero or more <see cref="FactorioObject"/>s. (But doesn't have to, if the user selects a close or cancel button.)
    /// </summary>
    /// <typeparam name="T">The type of result the panel can generate.</typeparam>
    public abstract class SelectObjectPanel<T> : PseudoScreenWithResult<T> {
        private readonly SearchableList<FactorioObject?> list;
        private string header = null!; // null-forgiving: set by Select
        private Rect searchBox;
        private string? noneTooltip;
        /// <summary>
        /// If <see langword="true"/> and the object being hovered is not a <see cref="Goods"/>, the <see cref="ObjectTooltip"/> should specify the type of object.
        /// See also <see cref="ObjectTooltipOptions.ShowTypeInHeader"/>.
        /// </summary>
        protected bool showTypeInHeader { get; private set; }

        protected SelectObjectPanel() : base(40f) => list = new SearchableList<FactorioObject?>(30, new Vector2(2.5f, 2.5f), ElementDrawer, ElementFilter);

        /// <summary>
        /// Opens a <see cref="SelectObjectPanel{T}"/> to allow the user to select zero or more <see cref="FactorioObject"/>s.
        /// </summary>
        /// <typeparam name="U"><see cref="FactorioObject"/> or one of its derived classes, to allow <paramref name="selectItem"/> and <paramref name="ordering"/> to have better type checking.</typeparam>
        /// <param name="list">The items to be displayed in this panel.</param>
        /// <param name="header">The string that describes to the user why they're selecting these items.</param>
        /// <param name="selectItem">An action to be called for each selected item when the panel is closed. The parameter may be <see langword="null"/> if <paramref name="allowNone"/> is <see langword="true"/>.</param>
        /// <param name="ordering">An optional ordering specifying how to sort the displayed items. If <see langword="null"/>, defaults to <see cref="DataUtils.DefaultOrdering"/>.</param>
        /// <param name="mapResult">An action that should convert the <typeparamref name="T"/>? result into zero or more <see cref="FactorioObject"/>s, and then call its second
        /// parameter for each <see cref="FactorioObject"/>. The first parameter may be <see langword="null"/> if <paramref name="allowNone"/> is <see langword="true"/>.</param>
        /// <param name="allowNone">If <see langword="true"/>, a "none" option will be displayed. Selection of this item will be conveyed by calling <paramref name="mapResult"/>
        /// and <paramref name="selectItem"/> with <see langword="default"/> values for <typeparamref name="T"/> and <typeparamref name="U"/>.</param>
        /// <param name="noneTooltip">If not <see langword="null"/>, this tooltip will be displayed when hovering over the "none" item.</param>
        protected void Select<U>(IEnumerable<U> list, string header, Action<U?> selectItem, IComparer<U>? ordering, Action<T?, Action<FactorioObject?>> mapResult, bool allowNone, string? noneTooltip = null) where U : FactorioObject {
            _ = MainScreen.Instance.ShowPseudoScreen(this);
            this.noneTooltip = noneTooltip;
            showTypeInHeader = typeof(U) == typeof(FactorioObject);
            List<U?> data = new List<U?>(list);
            ordering ??= DataUtils.DefaultOrdering;
            data.Sort(ordering!); // null-forgiving: We don't have any nulls in the list yet.
            if (allowNone) {
                data.Insert(0, null);
            }

            this.list.filter = default;
            this.list.data = data;
            this.header = header;
            Rebuild();
            completionCallback = (hasResult, result) => {
                if (hasResult) {
                    mapResult(result, obj => {
                        if (obj is U u) {
                            if (ordering is DataUtils.FavoritesComparer<U> favoritesComparer) {
                                favoritesComparer.AddToFavorite(u);
                            }

                            selectItem(u);
                        }
                        else if (allowNone) {
                            selectItem(null);
                        }
                    });
                }
            };
        }

        private void ElementDrawer(ImGui gui, FactorioObject? element, int index) {
            if (element == null) {
                ButtonEvent evt = gui.BuildRedButton(Icon.Close);
                if (noneTooltip != null) {
                    evt.WithTooltip(gui, noneTooltip);
                }
                if (evt) {
                    CloseWithResult(default);
                }
            }
            else {
                NonNullElementDrawer(gui, element);
            }
        }

        /// <summary>
        /// Called to draw a <see cref="FactorioObject"/> that should be displayed in this panel, and to handle mouse-over and click events.
        /// <paramref name="element"/> will not be null. If a "none" or "clear" option is present, <see cref="SelectObjectPanel{T}"/> takes care of that option.
        /// </summary>
        protected abstract void NonNullElementDrawer(ImGui gui, FactorioObject element);

        private bool ElementFilter(FactorioObject? data, SearchQuery query) => data?.Match(query) ?? true;

        public override void Build(ImGui gui) {
            BuildHeader(gui, header);
            if (gui.BuildSearchBox(list.filter, out var newFilter, "Start typing for search", setInitialFocus: true)) {
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
