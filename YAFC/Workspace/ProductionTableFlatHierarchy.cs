using System;
using System.Collections.Generic;
using YAFC.Model;
using YAFC.Parser;
using YAFC.UI;

namespace YAFC
{
    public class ProductionTableFlatHierarchy
    {
        private readonly DataGrid<RecipeRow> grid;
        private readonly List<object> flatHierarchy = new List<object>();
        private RecipeRow draggingRecipe;
        private ProductionTable root;

        public ProductionTableFlatHierarchy(DataGrid<RecipeRow> grid)
        {
            this.grid = grid;
        }

        public void SetData(ProductionTable table)
        {
            root = table;
            flatHierarchy.Clear();
            BuildFlatHierarchy(table);
        }

        private (ProductionTable, int) FindDragginRecipeParentAndIndex()
        {
            var index = flatHierarchy.IndexOf(draggingRecipe);
            if (index == -1)
                return default;
            var currentIndex = 0;
            for (var i = index - 1; i >= 0; i--)
            {
                if (flatHierarchy[i] is RecipeRow recipe)
                {
                    if (recipe.hasVisibleChildren)
                        return (recipe.subgroup, currentIndex);
                }
                else
                    i = flatHierarchy.LastIndexOf((flatHierarchy[i] as ProductionTable).owner, i);
                currentIndex++;
            }
            return (root, currentIndex);
        }

        private void ActuallyMoveDraggingRecipe()
        {
            var (parent, index) = FindDragginRecipeParentAndIndex();
            if (parent == null)
                return;
            if (draggingRecipe.owner == parent && parent.recipes[index] == draggingRecipe)
                return;

            draggingRecipe.owner.RecordUndo().recipes.Remove(draggingRecipe);
            draggingRecipe.SetOwner(parent);
            parent.RecordUndo().recipes.Insert(index, draggingRecipe);
        }

        private readonly List<(Rect, SchemeColor)> listBackgrounds = new List<(Rect, SchemeColor)>();
        private readonly Stack<float> depthStart = new Stack<float>();
        private void SwapBgColor(ref SchemeColor color) => color = color == SchemeColor.Background ? SchemeColor.PureBackground : SchemeColor.Background;
        public void Build(ImGui gui)
        {
            if (draggingRecipe != null && !gui.isDragging)
            {
                ActuallyMoveDraggingRecipe();
                draggingRecipe = null;
                SetData(root);
            }
            ProductionTable insideDraggingRecipe = null;
            grid.BeginBuildingContent(gui);
            var bgColor = SchemeColor.PureBackground;
            var depth = 0;
            var depWidth = 0f;
            var changed = false;
            for (var i = 0; i < flatHierarchy.Count; i++)
            {
                var item = flatHierarchy[i];
                if (item is RecipeRow recipe)
                {
                    if (recipe.hasVisibleChildren)
                    {
                        depth++;
                        SwapBgColor(ref bgColor);
                        depWidth = depth * 0.5f;
                        if (gui.isBuilding)
                            depthStart.Push(gui.statePosition.Bottom);
                    }
                    var rect = grid.BuildRow(gui, recipe, depWidth);
                    if (insideDraggingRecipe == null && gui.DoListReordering(rect, rect, recipe, out var from, bgColor, false))
                    {
                        draggingRecipe = from;
                        flatHierarchy.MoveListElement(from, recipe);    
                    }

                    if (gui.IsDragging(recipe))
                        insideDraggingRecipe = recipe.hasVisibleChildren ? recipe.subgroup : null;
                    if (recipe.hasVisibleChildren && recipe.subgroup.recipes.Count == 0)
                    {
                        using (gui.EnterGroup(new Padding(0.5f+depWidth, 0.5f, 0.5f, 0.5f)))
                        {
                            gui.BuildText("This is a nested group. You can drag&drop recipes here. Nested groups can have its own linked materials", wrap:true);
                            if (gui.BuildLink("Delete empty nested group"))
                            {
                                recipe.RecordUndo().subgroup = null;
                                changed = true;
                            }
                        }
                    }
                }
                else
                {   
                    if (gui.isBuilding)
                    {
                        var top = depthStart.Pop();
                        listBackgrounds.Add((new Rect(depWidth, top, grid.width - depWidth, gui.statePosition.Bottom - top), bgColor));
                    }
                    SwapBgColor(ref bgColor);
                    depth--;
                    depWidth = depth * 0.5f;
                    var footer = gui.AllocateRect(20f, 0.5f);
                    if (insideDraggingRecipe == null && gui.ConsumeDrag(footer.Center, item))
                    {
                        draggingRecipe = gui.GetDraggingObject<RecipeRow>();
                        flatHierarchy.MoveListElement(draggingRecipe, item);
                    }
                    if (insideDraggingRecipe == item)
                        insideDraggingRecipe = null;
                }
            }
            var fullRect = grid.EndBuildingContent(gui);
            if (gui.isBuilding)
            {
                foreach (var (rect, color) in listBackgrounds)
                    gui.DrawRectangle(rect, color, RectangleBorder.Thin);
                listBackgrounds.Clear();
            }
            gui.DrawRectangle(fullRect, SchemeColor.PureBackground);
            if (changed)
                SetData(root);
        }

        private void BuildFlatHierarchy(ProductionTable table)
        {
            foreach (var recipe in table.recipes)
            {
                flatHierarchy.Add(recipe);
                if (recipe.hasVisibleChildren)
                {
                    BuildFlatHierarchy(recipe.subgroup);
                    flatHierarchy.Add(recipe.subgroup);
                }
            }
        }

        public void BuildHeader(ImGui gui)
        {
            grid.BuildHeader(gui);
        }
    }
}