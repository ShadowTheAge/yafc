using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using SDL2;
using YAFC.Model;
using YAFC.UI;

namespace YAFC
{
    public class SelectObjectPanel : PseudoScreen<FactorioObject>
    {
        private static readonly SelectObjectPanel Instance = new SelectObjectPanel();
        private readonly SearchableList<FactorioObject> list;
        private string header;
        private Rect searchBox;
        private bool extendHeader;
        public SelectObjectPanel() : base(40f)
        {
            list = new SearchableList<FactorioObject>(30, new Vector2(2.5f, 2.5f), ElementDrawer, ElementFilter);
        }

        private bool ElementFilter(FactorioObject data, SearchQuery query) => data.Match(query);
        
        public static void Select<T>(IEnumerable<T> list, string header, Action<T> select, IComparer<T> ordering, bool allowNone) where T:FactorioObject
        {
            MainScreen.Instance.ShowPseudoScreen(Instance);
            Instance.extendHeader = typeof(T) == typeof(FactorioObject);
            var data = new List<T>(list);
            data.Sort(ordering);
            if (allowNone)
                data.Insert(0, null);
            Instance.list.filter = default;
            Instance.list.data = data;
            Instance.header = header;
            Instance.Rebuild();
            Instance.complete = (selected, x) =>
            {
                if (x is T t)
                {
                    if (ordering is DataUtils.FavouritesComparer<T> favouritesComparer)
                        favouritesComparer.AddToFavourite(t);
                    select(t);
                }
                else if (allowNone && selected)
                    select(null);
            };
        }

        public static void Select<T>(IEnumerable<T> list, string header, Action<T> select, bool allowNone = false) where T : FactorioObject => Select(list, header, select, DataUtils.DefaultOrdering, allowNone);

        private void ElementDrawer(ImGui gui, FactorioObject element, int index)
        {
            if (element == null)
            {
                if (gui.BuildRedButton(Icon.Close))
                    CloseWithResult(null);
            }
            else
            {
                if (gui.BuildFactorioObjectButton(element, display:MilestoneDisplay.Contained, extendHeader:extendHeader))
                    CloseWithResult(element);
            }
        }

        public override void Build(ImGui gui)
        {
            BuildHeader(gui, header);
            if (gui.BuildSearchBox(list.filter, out var newFilter, "Start typing for search"))
                list.filter = newFilter;
            searchBox = gui.lastRect;
            list.Build(gui);
        }

        public override bool KeyDown(SDL.SDL_Keysym key)
        {
            contents.SetTextInputFocus(searchBox, list.filter.query);
            return base.KeyDown(key);
        }
    }
}