using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using YAFC.Model;
using YAFC.UI;

namespace YAFC
{
    public class ObjectTooltip : Tooltip
    {
        private static readonly Padding contentPadding = new Padding(1f, 0.25f);
        
        public ObjectTooltip() : base(new Padding(0f, 0f, 0f, 0.5f), 25f) {}
        
        private IFactorioObjectWrapper target;

        private void BuildHeader(ImGui gui)
        {
            using (gui.EnterGroup(new Padding(1f, 0.5f), RectAllocator.LeftAlign, spacing:0f))
            {
                gui.BuildText(target.text, Font.header, true);
                using (gui.EnterRow(0f))
                {
                    foreach (var milestone in Milestones.milestones)
                    {
                        if (milestone[target.target])
                            gui.BuildIcon(milestone.obj.icon, 1f, SchemeColor.Source);
                    }
                }
            }
            if (gui.isBuilding)
                gui.DrawRectangle(gui.lastRect, SchemeColor.Primary);
        }

        private void BuildSubHeader(ImGui gui, string text)
        {
            using (gui.EnterGroup(contentPadding))
                gui.BuildText(text, Font.subheader);
            if (gui.isBuilding)
                gui.DrawRectangle(gui.lastRect, SchemeColor.Grey);
        }

        public static Rect BuildObjectIcon(ImGui gui, FactorioObject obj)
        {
            gui.BuildIcon(obj.icon, 2f, SchemeColor.Source);
            var rect = gui.lastRect;
            if (gui.action == ImGuiAction.Build)
            {
                var milestone = Milestones.GetHighest(obj);
                if (milestone != null)
                {
                    var milestoneIcon = new Rect(rect.BottomRight - new Vector2(0.5f, 0.5f), Vector2.One);
                    gui.DrawIcon(milestoneIcon, milestone.icon, SchemeColor.Source);
                }
            }
            return rect;
        }

        private void BuildIconRow(ImGui gui, IEnumerable<FactorioObject> objects, int maxCount = 10)
        {
            using (gui.EnterRow())
            {
                var count = 0;
                foreach (var icon in objects)
                {
                    BuildObjectIcon(gui, icon);
                    if (count++ >= maxCount)
                        break;
                }
                if (count == 0)
                    gui.BuildText("Nothing", color : SchemeColor.BackgroundTextFaint);
            }
        }

        private void BuildItem(ImGui gui, IFactorioObjectWrapper item)
        {
            using (gui.EnterRow())
            {
                BuildObjectIcon(gui, item.target);
                gui.BuildText(item.text, wrap:true);
            }
        }

        protected override void BuildContents(ImGui gui)
        {
            switch (target.target)
            {
                case Technology technology:
                    BuildTechnology(technology, gui);
                    break;
                case Recipe recipe:
                    BuildRecipe(recipe, gui);
                    break;
                case Goods goods:
                    BuildGoods(goods, gui);
                    break;
                case Entity entity:
                    BuildEntity(entity, gui);
                    break;
                default:
                    BuildCommon(target.target, gui);
                    break;
            }
        }

        private void BuildCommon(FactorioObject target, ImGui gui)
        {
            BuildHeader(gui);
            using (gui.EnterGroup(contentPadding))
            {
                if (target.locDescr != null)
                    gui.BuildText(target.locDescr, wrap:true);
                var complexity = target.GetComplexity();
                if (complexity != 0)
                    gui.BuildText("Complexity rating: " + Complexity.GetComplexityRatingName(complexity) + " ("+complexity+")");
                if (!target.IsAccessible())
                    gui.BuildText("This " + target.GetType().Name + " is inaccessible, or it is only accessible through mod or map script. Middle click to open dependency analyser to investigate.", wrap:true);
            }
        }

        private void BuildEntity(Entity entity, ImGui gui)
        {
            BuildCommon(entity, gui);
            
            if (entity.mapGenerated)
                gui.BuildText("Generates on map");
            
            if (entity.loot.Length > 0)
            {
                BuildSubHeader(gui, "Loot");
                using (gui.EnterGroup(contentPadding))
                {
                    foreach (var product in entity.loot)
                        BuildItem(gui, product);
                }
            }

            if (!entity.recipes.empty)
            {
                BuildSubHeader(gui, "Crafts");
                using (gui.EnterGroup(contentPadding))
                {
                    BuildIconRow(gui, entity.recipes);
                    gui.BuildText("Crafting speed: " + entity.craftingSpeed);
                }
            }

            if (entity.energy != null)
            {
                BuildSubHeader(gui, "Energy usage: "+entity.power+" MW");
                using (gui.EnterGroup(contentPadding))
                {
                    BuildIconRow(gui, entity.energy.fuels);
                    if (entity.energy.usesHeat)
                        gui.BuildText("Uses heat");
                }
            }
        }

        private void BuildGoods(Goods goods, ImGui gui)
        {
            BuildCommon(goods, gui);
            if (goods.production.Length > 0)
            {
                BuildSubHeader(gui, "Made with");
                using (gui.EnterGroup(contentPadding))
                    BuildIconRow(gui, goods.production);
            }
            
            if (goods.loot.Length > 0)
            {
                BuildSubHeader(gui, "Looted from");
                using (gui.EnterGroup(contentPadding))
                    BuildIconRow(gui, goods.loot);
            }

            if (goods.usages.Length > 0)
            {
                BuildSubHeader(gui, "Needs for");
                using (gui.EnterGroup(contentPadding))
                    BuildIconRow(gui, goods.usages);
            }

            if (goods.fuelValue != 0f)
            {
                BuildSubHeader(gui, "Fuel value: "+goods.fuelValue+" MJ");
                if (goods is Item item && item.fuelResult != null)
                    using (gui.EnterGroup(contentPadding))
                        BuildItem(gui, item.fuelResult);
            }

            if (goods is Item item2 && item2.placeResult != null)
            {
                BuildSubHeader(gui, "Place result");
                using (gui.EnterGroup(contentPadding))
                    BuildItem(gui, item2.placeResult);
            }
        }

        private void BuildRecipe(Recipe recipe, ImGui gui)
        {
            BuildCommon(recipe, gui);
            using (gui.EnterGroup(contentPadding, RectAllocator.LeftRow))
            {
                gui.BuildIcon(Icon.Time, 2f, SchemeColor.BackgroundText);
                gui.BuildText(recipe.time.ToString(CultureInfo.InvariantCulture));
            }
            using (gui.EnterGroup(contentPadding))
                foreach (var ingredient in recipe.ingredients)
                    BuildItem(gui, ingredient);

            if (recipe.products.Length > 0 && !(recipe.products.Length == 1 && recipe.products[0].amount == 1 && recipe.products[0].goods is Item && recipe.products[0].probability == 1f))
            {
                BuildSubHeader(gui, "Products");
                using (gui.EnterGroup(contentPadding))
                    foreach (var product in recipe.products)
                        BuildItem(gui, product);

            }

            BuildSubHeader(gui, "Made in");
            using (gui.EnterGroup(contentPadding))
                BuildIconRow(gui, recipe.crafters);
            
            if (!recipe.enabled)
            {
                BuildSubHeader(gui, "Unlocked by");
                using (gui.EnterGroup(contentPadding))
                {
                    BuildIconRow(gui, recipe.technologyUnlock);
                }
            }
        }

        private void BuildTechnology(Technology technology, ImGui gui)
        {
            BuildRecipe(technology, gui);
            if (technology.prerequisites.Length > 0)
            {
                BuildSubHeader(gui, "Prerequisites");
                using (gui.EnterGroup(contentPadding))
                    BuildIconRow(gui, technology.prerequisites);
            }
            
            if (technology.unlockRecipes.Length > 0)
            {
                BuildSubHeader(gui, "Unlocks recipes");
                using (gui.EnterGroup(contentPadding))
                    BuildIconRow(gui, technology.unlockRecipes);
            }
        }

        public void Show(IFactorioObjectWrapper target, ImGui gui, Rect rect)
        {
            this.target = target;
            base.SetFocus(gui, rect);
        }
    }
}