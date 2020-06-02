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

        private readonly VirtualScrollList<(FactorioObject, float)> list;
        private float shoppingCost = 0f;
        private bool decomposed = false;

        private ShoppingListScreen()
        {
            list = new VirtualScrollList<(FactorioObject, float)>(30f, new Vector2(float.PositiveInfinity, 2), ElementDrawer);
        }

        private void ElementDrawer(ImGui gui, (FactorioObject obj, float count) element, int index)
        {
            using (gui.EnterRow())
            {
                gui.BuildFactorioObjectIcon(element.obj, MilestoneDisplay.Contained);
                gui.RemainingRow().BuildText(DataUtils.FormatAmount(element.count, UnitOfMeasure.None, "x") + ": " + element.obj.locName);
            }
            gui.BuildFactorioObjectButton(gui.lastRect, element.obj);
        }

        public static void Show(Dictionary<FactorioObject, int> counts)
        {
            var cost = 0f;
            Instance.decomposed = false;
            Instance.list.data = counts.Select(x => (x.Key, Value:(float)x.Value)).OrderByDescending(x => x.Value).ToArray();
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
            using (gui.EnterRow(allocator:RectAllocator.RightRow))
            {
                if (gui.BuildButton("Done"))
                    Close();
                if (gui.BuildButton("Decompose", active: !decomposed))
                    Decompose();
            }
        }

        private Recipe FindSingleProduction(Recipe[] prodiuction)
        {
            Recipe current = null;
            foreach (var recipe in prodiuction)
            {
                if (recipe.IsAccessible())
                {
                    if (current != null)
                        return null;
                    current = recipe;
                }
            }

            return current;
        }

        private void Decompose()
        {
            decomposed = true;
            var decompositionQueue = new Queue<FactorioObject>();
            var decomposeResult = new Dictionary<FactorioObject, float>();

            void AddDecomposition(FactorioObject obj, float amount)
            {
                if (!decomposeResult.TryGetValue(obj, out var prev))
                    decompositionQueue.Enqueue(obj);
                decomposeResult[obj] = prev + amount;
            }

            foreach (var (item, count) in list.data)
                AddDecomposition(item, count);
            var steps = 0;
            while (decompositionQueue.Count > 0)
            {
                var elem = decompositionQueue.Dequeue();
                var amount = decomposeResult[elem];
                if (elem is Entity e && e.itemsToPlace.Count == 1)
                {
                    AddDecomposition(e.itemsToPlace.SingleOrNull(), amount);
                } 
                else if (elem is Recipe rec)
                {
                    foreach (var ingredient in rec.ingredients)
                        AddDecomposition(ingredient.goods, ingredient.amount * amount);
                } 
                else if (elem is Goods g && (g.usages.Length <= 5 || g is Item item && (item.factorioType != "item" || item.placeResult != null)) && (rec = FindSingleProduction(g.production)) != null)
                    AddDecomposition(g.production[0], amount / rec.GetProduction(g));
                else
                    continue;

                decomposeResult.Remove(elem);
                if (steps++ > 1000)
                    break;
            }
            
            list.data = decomposeResult.Select(x => (x.Key, x.Value)).OrderByDescending(x => x.Value).ToArray();
        }
    }
}