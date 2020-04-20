using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using YAFC.Model;
using YAFC.UI;

namespace YAFC
{
    public class ObjectTooltip : Tooltip
    {
        private FactorioObject target;

        private readonly FontString header = new FontString(Font.header);
        private FontStringPool strings = new FontStringPool(Font.text, true);
        private FontStringPool subheaders = new FontStringPool(Font.subheader, false);

        private void BuildString(LayoutState state, string text)
        {
            var fs = strings.Get();
            fs.text = text;
            state.Build(fs);
        }

        private void BuildSubHeader(LayoutState state, string text)
        {
            using (state.EnterGroup(new Padding(1f, 0.25f), RectAllocator.LeftAlign))
            {
                var fs = subheaders.Get();
                fs.text = text;
                state.Build(fs);
            }
            state.batch.DrawRectangle(state.lastRect, SchemeColor.BackgroundAlt);
        }

        private void BuildIconRow(LayoutState state, IEnumerable<FactorioObject> objects, int maxCount = 10)
        {
            using (state.EnterRow())
            {
                var count = 0;
                foreach (var icon in objects)
                {
                    state.BuildIcon(icon.icon, SchemeColor.Source, 2f);
                    if (count++ >= maxCount)
                        break;
                }
            }
        }

        private void BuildItem(LayoutState state, IFactorioObjectWrapper item)
        {
            using (state.EnterRow())
            {
                state.BuildIcon(item.target.icon, SchemeColor.Source, 2f);
                var lastRect = state.lastRect;
                var milestone = Milestones.GetHighest(item.target);
                if (milestone != null)
                {
                    var milestoneIcon = new Rect(lastRect.BottomRight - new Vector2(0.5f, 0.5f), Vector2.One);
                    state.batch.DrawIcon(milestoneIcon, milestone.icon, SchemeColor.Source);
                }
                BuildString(state, item.text);
            }
        }
        
        protected override void BuildContent(LayoutState state)
        {
            strings.Reset();
            subheaders.Reset();
            switch (target)
            {
                case Technology technology:
                    BuildTechnology(technology, state);
                    break;
                case Recipe recipe:
                    BuildRecipe(recipe, state);
                    break;
                case Goods goods:
                    BuildGoods(goods, state);
                    break;
                case Entity entity:
                    BuildEntity(entity, state);
                    break;
                default:
                    BuildCommon(target, state);
                    break;
            }
        }
        
        private void BuildCommon(FactorioObject target, LayoutState state)
        {
            using (state.EnterGroup(new Padding(1f, 0.5f), RectAllocator.LeftAlign))
            {
                header.text = target.locName;
                state.Build(header);
                
                using (state.EnterRow())
                {
                    foreach (var milestone in Milestones.milestones)
                    {
                        if (milestone[target])
                            state.BuildIcon(milestone.obj.icon, SchemeColor.Source);
                    }
                }
            }
            state.batch.DrawRectangle(state.lastRect, SchemeColor.Primary);
            
            if (target.locDescr != null)
                BuildString(state, target.locDescr);
            var complexity = target.GetComplexity();
            if (complexity != 0)
                BuildString(state, "Complexity rating: " + Complexity.GetComplexityRatingName(complexity) + " ("+complexity+")");
            if (!target.IsAccessible())
                BuildString(state, "This " + target.GetType().Name + " is inaccessible, or it is only accessible through mod or map script. Middle click to open dependency analyser to investigate.");
        }

        private void BuildEntity(Entity entity, LayoutState state)
        {
            BuildCommon(entity, state);
            
            if (entity.mapGenerated)
                BuildString(state, "Generates on map");
            
            if (entity.loot.Length > 0)
            {
                BuildSubHeader(state, "Loot");
                foreach (var product in entity.loot)
                    BuildItem(state, product);
            }

            if (!entity.recipes.empty)
            {
                BuildSubHeader(state, "Crafts");
                BuildIconRow(state, entity.recipes);
                BuildString(state, "Crafting speed: " + entity.craftingSpeed);
            }

            if (entity.energy != null)
            {
                BuildSubHeader(state, "Energy usage: "+entity.power+" MW");
                BuildIconRow(state, entity.energy.fuels);
                if (entity.energy.usesHeat)
                    BuildString(state, "Uses heat");
            }
        }

        private void BuildGoods(Goods goods, LayoutState state)
        {
            BuildCommon(goods, state);
            if (goods.production.Length > 0)
            {
                BuildSubHeader(state, "Made with");
                BuildIconRow(state, goods.production);
            }
            
            if (goods.loot.Length > 0)
            {
                BuildSubHeader(state, "Looted from");
                BuildIconRow(state, goods.loot);
            }

            if (goods.usages.Length > 0)
            {
                BuildSubHeader(state, "Needs for");
                BuildIconRow(state, goods.usages);
            }

            if (goods.fuelValue != 0f)
            {
                BuildSubHeader(state, "Fuel value: "+goods.fuelValue+" MJ");
                if (goods is Item item && item.fuelResult != null)
                    BuildItem(state, item.fuelResult);
            }

            if (goods is Item item2 && item2.placeResult != null)
            {
                BuildSubHeader(state, "Place result");
                BuildItem(state, item2.placeResult);
            }
        }

        private void BuildRecipe(Recipe recipe, LayoutState state)
        {
            BuildCommon(recipe, state);
            using (state.EnterRow())
            {
                state.BuildIcon(Icon.Time, SchemeColor.BackgroundText, 2f);
                BuildString(state, recipe.time.ToString(CultureInfo.InvariantCulture));
            }
            foreach (var ingredient in recipe.ingredients)
                BuildItem(state, ingredient);

            if (recipe.products.Length > 0 && !(recipe.products.Length == 1 && recipe.products[0].amount == 1 && recipe.products[0].goods is Item))
            {
                BuildSubHeader(state, "Products");
                foreach (var product in recipe.products)
                    BuildItem(state, product);

            }

            BuildSubHeader(state, "Made in");
            BuildIconRow(state, recipe.crafters);
            
            if (!recipe.enabled)
            {
                BuildSubHeader(state, "Unlocked by");
                BuildIconRow(state, recipe.technologyUnlock);
            }
        }

        private void BuildTechnology(Technology technology, LayoutState state)
        {
            BuildRecipe(technology, state);
            if (technology.prerequisites.Length > 0)
            {
                BuildSubHeader(state, "Prerequisites");
                BuildIconRow(state, technology.prerequisites);
            }
            
            if (technology.unlockRecipes.Length > 0)
            {
                BuildSubHeader(state, "Unlocks recipes");
                BuildIconRow(state, technology.unlockRecipes);
            }
        }

        public void Show(FactorioObject target, HitTestResult<IMouseHandle> hitTest)
        {
            this.target = target;
            ShowTooltip(hitTest);
        }
    }
}