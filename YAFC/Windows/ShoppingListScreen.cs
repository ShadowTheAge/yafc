using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using SDL2;
using YAFC.Blueprints;
using YAFC.Model;
using YAFC.UI;

namespace YAFC
{
    public class ShoppingListScreen : PseudoScreen
    {
        private static readonly ShoppingListScreen Instance = new ShoppingListScreen();

        private readonly VirtualScrollList<(FactorioObject, float)> list;
        private float shoppingCost, totalBuildings, totalModules;
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
            float cost = 0f, buildings = 0f, modules = 0f;
            Instance.decomposed = false;
            Instance.list.data = counts.Select(x => (x.Key, Value:(float)x.Value)).OrderByDescending(x => x.Value).ToArray();
            foreach (var (obj, count) in Instance.list.data)
            {
                if (obj is Entity)
                    buildings += count;
                else if (obj is Item item && item.module != null) 
                    modules += count;
                cost += obj.Cost() * count;
            }
            Instance.shoppingCost = cost;
            Instance.totalBuildings = buildings;
            Instance.totalModules = modules;
            MainScreen.Instance.ShowPseudoScreen(Instance);
        }
        
        public override void Build(ImGui gui)
        {
            BuildHeader(gui, "Shopping list");
            gui.BuildText(
                "Total cost of all objects: " + DataUtils.FormatAmount(shoppingCost, UnitOfMeasure.None, "¥") + ", buildings: " +
                DataUtils.FormatAmount(totalBuildings, UnitOfMeasure.None) + ", modules: " + DataUtils.FormatAmount(totalModules, UnitOfMeasure.None), align: RectAlignment.Middle);
            gui.AllocateSpacing(1f);
            list.Build(gui);
            using (gui.EnterRow(allocator:RectAllocator.RightRow))
            {
                if (gui.BuildButton("Done"))
                    Close();
                if (gui.BuildButton("Decompose", active: !decomposed))
                    Decompose();
                if (gui.BuildButton("Export to blueprint", SchemeColor.Grey))
                    gui.ShowDropDown(ExportBlueprintDropdown);
            }
        }

        private List<(T, int)> ExportGoods<T>() where T:Goods
        {
            var items = new List<(T, int)>();
            foreach (var (element, amount) in list.data)
            {
                var rounded = MathUtils.Round(amount);
                if (rounded == 0)
                    continue;
                if (element is T g)
                    items.Add((g, rounded));
                else if (element is Entity e && e.itemsToPlace.Count > 0)
                    items.Add((e.itemsToPlace[0] as T, rounded));
            }

            return items;
        }

        private void ExportBlueprintDropdown(ImGui gui, ref bool closed)
        {
            gui.BuildText("Blueprint string will be copied to clipboard", wrap:true);
            if (Database.objectsByTypeName.TryGetValue("Entity.constant-combinator", out var combinator) && gui.BuildFactorioObjectButtonWithText(combinator) && (closed = true))
                BlueprintUtilities.ExportConstantCombinators("Shopping list", ExportGoods<Goods>());
            foreach (var container in Database.allContainers)
            {
                if (container.logisticMode == "requester" && gui.BuildFactorioObjectButtonWithText(container) && (closed = true))
                    BlueprintUtilities.ExportRequesterChests("Shopping list", ExportGoods<Item>(), container);
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
                    if (rec.HasIngredientVariants())
                        continue;
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