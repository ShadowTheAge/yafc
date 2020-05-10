using System;
using System.Collections.Generic;
using YAFC.Model;
using YAFC.UI;

namespace YAFC
{
    public class AutoPlannerView : ProjectPageView<AutoPlanner>
    {
        private AutoPlannerRecipe selectedRecipe;
        public override void SetModel(ProjectPage page)
        {
            base.SetModel(page);
            selectedRecipe = null;
        }

        private Action CreateAutoPlannerWizard(List<WizardPanel.PageBuilder> pages)
        {
            var goal = new List<AutoPlannerGoal>();
            var pageName = "Auto planner";

            void Page1(ImGui gui, ref bool valid)
            {
                gui.BuildText("Enter page name:");
                gui.BuildTextInput(pageName, out pageName, null);
                gui.AllocateSpacing(2f);
                gui.BuildText("Select your goal and amount per second:");
                using (var grid = gui.EnterInlineGrid(3f))
                {
                    for (var i = 0; i < goal.Count; i++)
                    {
                        var elem = goal[i];
                        grid.Next();
                        var evt = gui.BuildFactorioGoodsWithEditableAmount(elem.item, elem.amount, out var newAmount);
                        if (evt == GoodsWithAmountEvent.TextEditing)
                        {
                            if (newAmount != 0f)
                                elem.amount = newAmount;
                            else goal.RemoveAt(i--);
                        }
                    }
                    grid.Next();
                    if (gui.BuildButton(Icon.Plus, SchemeColor.Primary, SchemeColor.PrimalyAlt, size:2.5f))
                    {
                        SelectObjectPanel.Select(Database.allGoods, "New production goal", x =>
                        {
                            goal.Add(new AutoPlannerGoal {amount = 1f, item = x});
                            gui.Rebuild();
                        });
                    }
                    grid.Next();
                }
                gui.AllocateSpacing(2f);
                gui.BuildText("Review active milestones, as they will restrict recipes that are considered:", wrap:true);
                MilestonesWidget.Instance.Build(gui);
                gui.AllocateSpacing(2f);
                valid = !string.IsNullOrEmpty(pageName) && goal.Count > 0;
            }
            
            pages.Add(Page1);
            return () =>
            {
                var planner = MainScreen.Instance.AddProjectPageAndSetActive<AutoPlanner>("Auto planner", goal[0].item);
                planner.goals.AddRange(goal);
            };
        }
        public override void BuildHeader(ImGui gui)
        {
            
        }

        public override void BuildContent(ImGui gui)
        {
            if (model.tiers == null)
                return;
            foreach (var tier in model.tiers)
            {
                using (var grid = gui.EnterInlineGrid(3f))
                {
                    foreach (var recipe in tier)
                    {
                        var color = SchemeColor.None;
                        if (gui.isBuilding)
                        {
                            if (selectedRecipe != null && (selectedRecipe.downstream != null && selectedRecipe.downstream.Contains(recipe.recipe) ||
                                                           selectedRecipe.upstream != null && selectedRecipe.upstream.Contains(recipe.recipe)))
                                color = SchemeColor.Secondary;
                        }
                        grid.Next();
                        if (gui.BuildFactorioObjectWithAmount(recipe.recipe, recipe.recipesPerSecond, color))
                            selectedRecipe = recipe;
                    }
                }
            }
        }

        public override void CreateModelDropdown(ImGui gui, Type type, Project project, ref bool close)
        {
            if (gui.BuildButton("Auto planner"))
            {
                close = true;
                WizardPanel.Show("New auto planner", CreateAutoPlannerWizard);
            }
        }
    }
}