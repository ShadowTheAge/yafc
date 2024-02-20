using System;
using System.Collections.Generic;
using YAFC.Model;
using YAFC.UI;

namespace YAFC {
    public class FlatHierarchy<TRow, TGroup> where TRow : ModelObject<TGroup>, IGroupedElement<TGroup> where TGroup : ModelObject<ModelObject>, IElementGroup<TRow> {
        private readonly DataGrid<TRow> grid;
        private readonly List<TRow> flatRecipes = new List<TRow>();
        private readonly List<TGroup> flatGroups = new List<TGroup>();
        private TRow draggingRecipe;
        private TGroup root;
        private bool rebuildRequired;
        private readonly Action<ImGui, TGroup> drawTableHeader;
        private readonly string emptyGroupMessage;
        private readonly bool buildExpandedGroupRows;

        public FlatHierarchy(DataGrid<TRow> grid, Action<ImGui, TGroup> drawTableHeader, string emptyGroupMessage = "This is an empty group", bool buildExpandedGroupRows = true) {
            this.grid = grid;
            this.drawTableHeader = drawTableHeader;
            this.emptyGroupMessage = emptyGroupMessage;
            this.buildExpandedGroupRows = buildExpandedGroupRows;
        }

        public float width => grid.width;
        public void SetData(TGroup table) {
            root = table;
            rebuildRequired = true;
        }

        private (TGroup, int) FindDragginRecipeParentAndIndex() {
            var index = flatRecipes.IndexOf(draggingRecipe);
            if (index == -1)
                return default;
            var currentIndex = 0;
            for (var i = index - 1; i >= 0; i--) {
                if (flatRecipes[i] is TRow recipe) {
                    var group = recipe.subgroup;
                    if (group != null && group.expanded)
                        return (group, currentIndex);
                }
                else
                    i = flatRecipes.LastIndexOf(flatGroups[i].owner as TRow, i);
                currentIndex++;
            }
            return (root, currentIndex);
        }

        private void ActuallyMoveDraggingRecipe() {
            var (parent, index) = FindDragginRecipeParentAndIndex();
            if (parent == null)
                return;
            if (draggingRecipe.owner == parent && parent.elements[index] == draggingRecipe)
                return;

            draggingRecipe.owner.RecordUndo().elements.Remove(draggingRecipe);
            draggingRecipe.SetOwner(parent);
            parent.RecordUndo().elements.Insert(index, draggingRecipe);
        }

        private void MoveFlatHierarchy(TRow from, TRow to) {
            draggingRecipe = from;
            var indexFrom = flatRecipes.IndexOf(from);
            var indexTo = flatRecipes.IndexOf(to);
            flatRecipes.MoveListElementIndex(indexFrom, indexTo);
            flatGroups.MoveListElementIndex(indexFrom, indexTo);
        }

        private void MoveFlatHierarchy(TRow from, TGroup to) {
            draggingRecipe = from;
            var indexFrom = flatRecipes.IndexOf(from);
            var indexTo = flatGroups.IndexOf(to);
            flatRecipes.MoveListElementIndex(indexFrom, indexTo);
            flatGroups.MoveListElementIndex(indexFrom, indexTo);
        }

        //private readonly List<(Rect, SchemeColor)> listBackgrounds = new List<(Rect, SchemeColor)>();
        private readonly Stack<float> depthStart = new Stack<float>();
        private void SwapBgColor(ref SchemeColor color) => color = color == SchemeColor.Background ? SchemeColor.PureBackground : SchemeColor.Background;
        public void Build(ImGui gui) {
            if (draggingRecipe != null && !gui.isDragging) {
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
            for (var i = 0; i < flatRecipes.Count; i++) {
                var recipe = flatRecipes[i];
                var item = flatGroups[i];
                if (recipe != null) {
                    if (!recipe.visible) {
                        if (item != null)
                            i = flatGroups.LastIndexOf(item);
                        continue;
                    }

                    if (item != null) {
                        depth++;
                        SwapBgColor(ref bgColor);
                        depWidth = depth * 0.5f;
                        if (gui.isBuilding)
                            depthStart.Push(gui.statePosition.Bottom);
                    }

                    if (buildExpandedGroupRows || item == null) {
                        var rect = grid.BuildRow(gui, recipe, depWidth);
                        if (item == null && gui.InitiateDrag(rect, rect, recipe, bgColor))
                            draggingRecipe = recipe;
                        else if (gui.ConsumeDrag(rect.Center, recipe))
                            MoveFlatHierarchy(gui.GetDraggingObject<TRow>(), recipe);
                    }
                    if (item != null) {
                        if (item.elements.Count == 0) {
                            using (gui.EnterGroup(new Padding(0.5f + depWidth, 0.5f, 0.5f, 0.5f))) {
                                gui.BuildText(emptyGroupMessage, wrap: true);
                            }
                        }

                        if (drawTableHeader != null) {
                            using (gui.EnterGroup(new Padding(0.5f + depWidth, 0.5f, 0.5f, 0.5f)))
                                drawTableHeader(gui, item);
                        }
                    }
                }
                else {
                    if (gui.isBuilding) {
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

        private void Rebuild() {
            flatRecipes.Clear();
            flatGroups.Clear();
            BuildFlatHierarchy(root);
            rebuildRequired = false;
        }

        private void BuildFlatHierarchy(TGroup table) {
            foreach (var recipe in table.elements) {
                flatRecipes.Add(recipe);
                var sub = recipe.subgroup;
                if (sub != null && sub.expanded) {
                    flatGroups.Add(sub);
                    BuildFlatHierarchy(sub);
                    flatRecipes.Add(null);
                    flatGroups.Add(sub);
                }
                else flatGroups.Add(null);
            }
        }

        public void BuildHeader(ImGui gui) {
            grid.BuildHeader(gui);
        }
    }
}