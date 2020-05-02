using System;
using System.Collections.Generic;
using System.ComponentModel;
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
        private string filter;
        private string header;
        private Rect searchBox;
        public SelectObjectPanel() : base(40f)
        {
            list = new SearchableList<FactorioObject>(30, new Vector2(2.5f, 2.5f), ElementDrawer, ElementFilter);
        }

        private bool ElementFilter(FactorioObject data, string[] searchTokens)
        {
            foreach (var token in searchTokens)
            {   
                if (data.name.IndexOf(token, StringComparison.OrdinalIgnoreCase) < 0 &&
                    data.locName.IndexOf(token, StringComparison.OrdinalIgnoreCase) < 0 &&
                    (data.locDescr == null || data.locDescr.IndexOf(token, StringComparison.OrdinalIgnoreCase) < 0)) 
                    return false;
            }

            return true;
        }
        
        public static void Select<T>(IEnumerable<T> list, string header, Action<T> select, IComparer<T> ordering) where T:FactorioObject
        {
            MainScreen.Instance.ShowPseudoScreen(Instance);
            var data = new List<T>(list);
            data.Sort(ordering);
            Instance.list.data = data;
            Instance.header = header;
            Instance.Rebuild();
            Instance.complete = x =>
            {
                if (x is T t)
                    select(t);
            };
        }

        public static void Select<T>(IEnumerable<T> list, string header, Action<T> select) where T : FactorioObject => Select(list, header, select, DataUtils.DefaultOrdering);

        private void ElementDrawer(ImGui gui, FactorioObject element, int index)
        {
            if (gui.BuildFactorioObjectButton(element, contain:true))
                CloseWithResult(element);
        }

        public override void Build(ImGui gui)
        {
            gui.BuildText(header, Font.header, align: RectAlignment.Middle);
            if (gui.BuildTextInput(filter, out filter, "Start typing for search", icon:Icon.Search))
                list.filter = filter;
            searchBox = gui.lastRect;
            list.Build(gui);
        }

        public override void KeyDown(SDL.SDL_Keysym key)
        {
            base.KeyDown(key);
            contents.SetTextInputFocus(searchBox);
        }
    }
}