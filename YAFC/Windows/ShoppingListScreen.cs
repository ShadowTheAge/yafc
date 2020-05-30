using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using YAFC.Model;
using YAFC.UI;

namespace YAFC
{
    public class ShoppingListScreen : PseudoScreen
    {
        private static readonly ShoppingListScreen Instance = new ShoppingListScreen();

        private readonly VirtualScrollList<(FactorioObject, int)> list;
        private float shoppingCost = 0f;

        private ShoppingListScreen()
        {
            list = new VirtualScrollList<(FactorioObject, int)>(30f, new Vector2(float.PositiveInfinity, 2), ElementDrawer);
        }

        private void ElementDrawer(ImGui gui, (FactorioObject obj, int count) element, int index)
        {
            using (gui.EnterRow())
            {
                gui.BuildFactorioObjectIcon(element.obj, MilestoneDisplay.Contained);
                gui.RemainingRow().BuildText(element.count + "x " + element.obj.locName);
            }
            gui.BuildFactorioObjectButton(gui.lastRect, element.obj);
        }

        public static void Show(Dictionary<FactorioObject, int> counts)
        {
            var cost = 0f;
            Instance.list.data = counts.Select(x => (x.Key, x.Value)).OrderByDescending(x => x.Value).ToArray();
            foreach (var (obj, count) in Instance.list.data)
                cost += obj.Cost() * count;
            Instance.shoppingCost = cost;
            MainScreen.Instance.ShowPseudoScreen(Instance);
        }
        
        public override void Build(ImGui gui)
        {
            BuildHeader(gui, "Shopping list");
            gui.BuildText("Total cost of all objects: "+DataUtils.FormatAmount(shoppingCost, UnitOfMeasure.None, "¥"), align:RectAlignment.Middle);
            gui.AllocateSpacing(1f);
            list.Build(gui);
        }
    }
}