using System;
using System.Collections.Generic;
using YAFC.Model;
using YAFC.UI;

namespace YAFC {
    /// <summary>
    /// <para>
    /// This is a flat hierarchy that can be used to display a table with nested groups in a single list.
    /// The method <see cref="BuildFlatHierarchy"/> flattens the tree.
    /// </para>
    /// <para>
    /// This class interacts tightly with <see cref="ProductionTableView"/>. The computation of the rendering
    /// state occurs at multiple stages, resulting in some interleaving. This is due to YAFC utilizing an
    /// intermediate GUI architecture with loop-based rendering via SDL instead of employing an event-based
    /// GUI system.
    /// </para>
    /// </summary>
    public class FlatHierarchy<TRow, TGroup> where TRow : ModelObject<TGroup>, IGroupedElement<TGroup> where TGroup : ModelObject<ModelObject>, IElementGroup<TRow> {
        private readonly DataGrid<TRow> grid;
        private readonly List<TRow> flatRecipes = new List<TRow>();
        private readonly List<TGroup> flatGroups = new List<TGroup>();
        private readonly List<RowHighlighting> rowHighlighting = new List<RowHighlighting>();
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

        private (TGroup, int) FindDraggingRecipeParentAndIndex() {
            int index = flatRecipes.IndexOf(draggingRecipe);
            if (index == -1) {
                return default;
            }

            int currentIndex = 0;
            for (int i = index - 1; i >= 0; i--) {
                if (flatRecipes[i] is TRow recipe) {
                    var group = recipe.subgroup;
                    if (group != null && group.expanded) {
                        return (group, currentIndex);
                    }
                }
                else {
                    i = flatRecipes.LastIndexOf(flatGroups[i].owner as TRow, i);
                }

                currentIndex++;
            }
            return (root, currentIndex);
        }

        private void ActuallyMoveDraggingRecipe() {
            var (parent, index) = FindDraggingRecipeParentAndIndex();
            if (parent == null) {
                return;
            }

            if (draggingRecipe.owner == parent && parent.elements[index] == draggingRecipe) {
                return;
            }

            _ = draggingRecipe.owner.RecordUndo().elements.Remove(draggingRecipe);
            draggingRecipe.SetOwner(parent);
            parent.RecordUndo().elements.Insert(index, draggingRecipe);
        }

        private void MoveFlatHierarchy(TRow from, TRow to) {
            draggingRecipe = from;
            int indexFrom = flatRecipes.IndexOf(from);
            int indexTo = flatRecipes.IndexOf(to);
            flatRecipes.MoveListElementIndex(indexFrom, indexTo);
            flatGroups.MoveListElementIndex(indexFrom, indexTo);
        }

        private void MoveFlatHierarchy(TRow from, TGroup to) {
            draggingRecipe = from;
            int indexFrom = flatRecipes.IndexOf(from);
            int indexTo = flatGroups.IndexOf(to);
            flatRecipes.MoveListElementIndex(indexFrom, indexTo);
            flatGroups.MoveListElementIndex(indexFrom, indexTo);
        }

        private readonly Stack<float> depthStart = new Stack<float>();

        /// <summary>
        /// This method alternates between the two row background colors, which in turn increase readability.
        /// If a row is highlighted, the background color is set to the highlighting color without alternation.
        /// </summary>
        /// <param name="color">Current background color</param>
        private void SwapBgColor(ref SchemeColor color) {
            if (nextRowIsHighlighted) {
                color = nextRowBackgroundColor;
            }
            else {
                color = (color == SchemeColor.Background) ? SchemeColor.PureBackground : SchemeColor.Background;
            }
        }

        /// <summary>
        /// This property is utilized by the rendering code to determine whether a row that will be drawn
        /// requires highlighting.
        /// </summary>
        public bool nextRowIsHighlighted { get; private set; }

        public SchemeColor nextRowBackgroundColor { get; private set; }
        public SchemeColor nextRowTextColor { get; private set; }

        public void Build(ImGui gui) {
            if (draggingRecipe != null && !gui.isDragging) {
                ActuallyMoveDraggingRecipe();
                draggingRecipe = null;
                rebuildRequired = true;
            }
            if (rebuildRequired) {
                Rebuild();
            }

            grid.BeginBuildingContent(gui);
            var bgColor = SchemeColor.PureBackground;
            int depth = 0;
            float depWidth = 0f;
            for (int i = 0; i < flatRecipes.Count; i++) {
                var recipe = flatRecipes[i];
                var item = flatGroups[i];

                nextRowIsHighlighted = (typeof(TRow) == typeof(RecipeRow)) && (rowHighlighting[i] != RowHighlighting.None);

                // TODO: See https://github.com/have-fun-was-taken/yafc-ce/issues/91
                //       and https://github.com/have-fun-was-taken/yafc-ce/pull/86#discussion_r1550369353
                if (nextRowIsHighlighted) {
                    nextRowBackgroundColor = GetHighlightingBackgroundColor(rowHighlighting[i], recipe is RecipeRow { enabled: true });
                    nextRowTextColor = GetHighlightingTextColor(rowHighlighting[i]);
                }
                else {
                    nextRowBackgroundColor = bgColor;
                    nextRowTextColor = SchemeColor.BackgroundText;
                }

                bool isError = recipe is RecipeRow r && r.parameters.warningFlags >= WarningFlags.EntityNotSpecified;
                if (isError) {
                    nextRowBackgroundColor = SchemeColor.Error;
                    nextRowTextColor = SchemeColor.PureForeground;
                }

                if (recipe != null) {
                    if (!recipe.visible) {
                        if (item != null) {
                            i = flatGroups.LastIndexOf(item);
                        }

                        continue;
                    }

                    if (item != null) {
                        depth++;
                        SwapBgColor(ref bgColor);
                        depWidth = depth * 0.5f;
                        if (gui.isBuilding) {
                            depthStart.Push(gui.statePosition.Bottom);
                        }
                    }

                    if (buildExpandedGroupRows || item == null) {
                        var rect = grid.BuildRow(gui, recipe, depWidth);
                        if (item == null && gui.InitiateDrag(rect, rect, recipe, bgColor)) {
                            draggingRecipe = recipe;
                        }
                        else if (gui.ConsumeDrag(rect.Center, recipe)) {
                            MoveFlatHierarchy(gui.GetDraggingObject<TRow>(), recipe);
                        }

                        if (nextRowIsHighlighted || isError) {
                            rect.X += depWidth;
                            rect.Width -= depWidth;
                            gui.DrawRectangle(rect, nextRowBackgroundColor);
                        }
                    }
                    if (item != null) {
                        if (item.elements.Count == 0) {
                            using (gui.EnterGroup(new Padding(0.5f + depWidth, 0.5f, 0.5f, 0.5f))) {
                                gui.BuildText(emptyGroupMessage, wrap: true); // set color if the nested row is empty
                            }
                        }

                        if (drawTableHeader != null) {
                            using (gui.EnterGroup(new Padding(0.5f + depWidth, 0.5f, 0.5f, 0.5f))) {
                                drawTableHeader(gui, item);
                            }
                        }
                    }
                }
                else {
                    if (gui.isBuilding) {
                        float top = depthStart.Pop();
                        // set color bgColor if the row is a nested table and is not collapsed
                        gui.DrawRectangle(new Rect(depWidth, top, grid.width - depWidth, gui.statePosition.Bottom - top), bgColor, RectangleBorder.Thin);
                    }
                    SwapBgColor(ref bgColor);
                    depth--;
                    depWidth = depth * 0.5f;
                    _ = gui.AllocateRect(20f, 0.5f);
                }
            }
            var fullRect = grid.EndBuildingContent(gui);
            gui.DrawRectangle(fullRect, SchemeColor.PureBackground);
        }

        private void Rebuild() {
            flatRecipes.Clear();
            flatGroups.Clear();
            rowHighlighting.Clear();

            BuildFlatHierarchy(root);
            rebuildRequired = false;
        }

        private void BuildFlatHierarchy(TGroup table, RowHighlighting parentHighlight = RowHighlighting.None) {
            foreach (var recipe in table.elements) {
                flatRecipes.Add(recipe);

                RowHighlighting highlight = parentHighlight;

                if (recipe is RecipeRow r && r.highlighting != RowHighlighting.None) {
                    // Only respect any highlighting if the recipe is in fact a RecipeRow
                    highlight = r.highlighting;
                }

                rowHighlighting.Add(highlight);

                var sub = recipe.subgroup;

                if (sub is { expanded: true }) {
                    flatGroups.Add(sub);

                    BuildFlatHierarchy(sub, highlight); // Pass the current highlight color to the child rows

                    // The flattened hierarchy contains empty rows for the nested table. But since this
                    // is a rendering issue, it is not necessary to add them to the actual data structure.
                    // TODO: Investigate why this is needed and how to clean it up.
                    flatRecipes.Add(null);
                    flatGroups.Add(sub);
                    rowHighlighting.Add(RowHighlighting.None);
                }
                else {
                    flatGroups.Add(null);
                }
            }
        }

        public void BuildHeader(ImGui gui) {
            grid.BuildHeader(gui);
        }

        /// <summary>
        /// This method is used to determine the background color for a highlighted row. To avoid
        /// excessive coupling, the tag state and the row color are kept separate.
        /// </summary>
        /// <param name="highlighting">
        /// Represents the highlighting state for which the corresponding color needs to be determined.
        /// </param>
        /// <param name="isEnabled">
        /// If the row is not enabled, a fainter color is used.
        /// </param>
        /// <returns></returns>
        private static SchemeColor GetHighlightingBackgroundColor(RowHighlighting highlighting, bool isEnabled) {
            return highlighting switch {
                RowHighlighting.Green => isEnabled ? SchemeColor.TagColorGreen : SchemeColor.TagColorGreenAlt,
                RowHighlighting.Yellow => isEnabled ? SchemeColor.TagColorYellow : SchemeColor.TagColorYellowAlt,
                RowHighlighting.Red => isEnabled ? SchemeColor.TagColorRed : SchemeColor.TagColorRedAlt,
                RowHighlighting.Blue => isEnabled ? SchemeColor.TagColorBlue : SchemeColor.TagColorBlueAlt,
                _ => SchemeColor.None
            };
        }

        /// <summary>
        /// This method is used to determine the text color for a highlighted row. To avoid
        /// excessive coupling, the tag state and the row color are kept separate.
        /// </summary>
        /// <param name="highlighting">
        /// Represents the highlighting state for which the corresponding color needs to be determined.
        /// </param>
        /// <returns></returns>
        private static SchemeColor GetHighlightingTextColor(RowHighlighting highlighting) {
            return highlighting switch {
                RowHighlighting.Green => SchemeColor.TagColorGreenText,
                RowHighlighting.Yellow => SchemeColor.TagColorYellowText,
                RowHighlighting.Red => SchemeColor.TagColorRedText,
                RowHighlighting.Blue => SchemeColor.TagColorBlueText,
                _ => SchemeColor.None
            };
        }
    }
}
