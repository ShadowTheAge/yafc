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
        private readonly List<RecipeRow> flatRecipes = new List<RecipeRow>();
        private readonly List<ProductionTable> flatGroups = new List<ProductionTable>();
        private RecipeRow draggingRecipe;
        private ProductionTable root;
        private bool rebuildRequired;
        private readonly Action<ImGui, ProductionTable> drawTableHeader;

        public ProductionTableFlatHierarchy(DataGrid<RecipeRow> grid, Action<ImGui, ProductionTable> drawTableHeader)
        {
            this.grid = grid;
            this.drawTableHeader = drawTableHeader;
        }

        public float width => grid.width;
        public void SetData(ProductionTable table)
        {
            root = table;
            rebuildRequired = true;
        }

        private (ProductionTable, int) FindDragginRecipeParentAndIndex()
        {
            var index = flatRecipes.IndexOf(draggingRecipe);
            if (index == -1)
                return default;
            var currentIndex = 0;
            for (var i = index - 1; i >= 0; i--)
            {
                if (flatRecipes[i] is RecipeRow recipe)
                {
                    if (recipe.hasVisibleChildren)
                        return (recipe.subgroup, currentIndex);
                }
                else
                    i = flatRecipes.LastIndexOf(flatGroups[i].owner as RecipeRow, i);
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

        private void MoveFlatHierarchy(RecipeRow from, RecipeRow to)
        {
            draggingRecipe = from;
            var indexFrom = flatRecipes.IndexOf(from);
            var indexTo = flatRecipes.IndexOf(to);
            flatRecipes.MoveListElementIndex(indexFrom, indexTo);
            flatGroups.MoveListElementIndex(indexFrom, indexTo);
        }
        
        private void MoveFlatHierarchy(RecipeRow from, ProductionTable to)
        {
            draggingRecipe = from;
            var indexFrom = flatRecipes.IndexOf(from);
            var indexTo = flatGroups.IndexOf(to);
            flatRecipes.MoveListElementIndex(indexFrom, indexTo);
            flatGroups.MoveListElementIndex(indexFrom, indexTo);
        }

        //private readonly List<(Rect, SchemeColor)> listBackgrounds = new List<(Rect, SchemeColor)>();
        private readonly Stack<float> depthStart = new Stack<float>();
        private void SwapBgColor(ref SchemeColor color) => color = color == SchemeColor.Background ? SchemeColor.PureBackground : SchemeColor.Background;
        public void Build(ImGui gui)
        {
            if (draggingRecipe != null && !gui.isDragging)
            {
                ActuallyMoveDraggingRecipe();
                draggingRecipe = null;
                rebuildRequired = true;
            }
            if (rebuildRequired)
                Rebuild();
            
            grid.BeginBuildingContent(gui);
            var bgColor = SchemeColor.PureBackground;
            var depth = 0;
            var depWidth = 0f;
            for (var i = 0; i < flatRecipes.Count; i++)
            {
                var recipe = flatRecipes[i];
                var item = flatGroups[i];
                if (recipe != null)
                {
                    if (item != null)
                    {
                        depth++;
                        SwapBgColor(ref bgColor);
                        depWidth = depth * 0.5f;
                        if (gui.isBuilding)
                            depthStart.Push(gui.statePosition.Bottom);
                    }
                    var rect = grid.BuildRow(gui, recipe, depWidth);
                    if (item == null && gui.InitiateDrag(rect, rect, recipe, bgColor))
                        draggingRecipe = recipe;
                    else if (gui.ConsumeDrag(rect.Center, recipe))
                        MoveFlatHierarchy(gui.GetDraggingObject<RecipeRow>(), recipe);
                    if (item != null)
                    {
                        if (item.recipes.Count == 0)
                        {
                            using (gui.EnterGroup(new Padding(0.5f+depWidth, 0.5f, 0.5f, 0.5f)))
                            {
                                gui.BuildText("This is a nested group. You can drag&drop recipes here. Nested groups can have its own linked materials", wrap:true);
                                if (gui.BuildLink("Delete empty nested group"))
                                {
                                    recipe.RecordUndo().subgroup = null;
                                    rebuildRequired = true;
                                }
                            }
                        }
                        using (gui.EnterGroup(new Padding(0.5f+depWidth, 0.5f, 0.5f, 0.5f)))
                            drawTableHeader(gui, item);
                    }
                }
                else
                {
                    if (gui.isBuilding)
                    {
                        var top = depthStart.Pop();
                        gui.DrawRectangle(new Rect(depWidth, top, grid.width - depWidth, gui.statePosition.Bottom - top), bgColor, RectangleBorder.Thin);
                    }
                    SwapBgColor(ref bgColor);
                    depth--;
                    depWidth = depth * 0.5f;
                    gui.AllocateRect(20f, 0.5f);
                }
            }
            var fullRect = grid.EndBuildingContent(gui);
            gui.DrawRectangle(fullRect, SchemeColor.PureBackground);
        }
        
        private void Rebuild()
        {
            flatRecipes.Clear();
            flatGroups.Clear();
            BuildFlatHierarchy(root);
            rebuildRequired = false;
        }

        private void BuildFlatHierarchy(ProductionTable table)
        {
            foreach (var recipe in table.recipes)
            {
                flatRecipes.Add(recipe);
                if (recipe.hasVisibleChildren)
                {
                    flatGroups.Add(recipe.subgroup);
                    BuildFlatHierarchy(recipe.subgroup);
                    flatRecipes.Add(null);
                    flatGroups.Add(recipe.subgroup);
                }
                else flatGroups.Add(null);
            }
        }

        public void BuildHeader(ImGui gui)
        {
            grid.BuildHeader(gui);
        }
    }
}