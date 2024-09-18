using System;
using System.Collections.Generic;
using System.Linq;
using Yafc.Model;
using Yafc.UI;

namespace Yafc;

public partial class MainScreen {
    /// <summary>
    /// Encapsulates the text box and checkboxes used for searching in the page list dropdown.
    /// </summary>
    private class PageListSearch {
        private SearchQuery query;

        // The state of the checkboxes. To add a new checkbox: Add a new value to PageSearchOption,
        // draw the new checkbox in Build, and obey the new checkbox in Search.
        private readonly bool[] checkboxValues = new bool[(int)PageSearchOption.MustBeLastValue];
        private readonly bool[] previousCheckboxValues = new bool[(int)PageSearchOption.MustBeLastValue];
        // Named constants for which bool means which type of search.
        private enum PageSearchOption {
            PageName,
            DesiredProducts,
            ExtraProducts,
            Ingredients,
            Recipes,
            // Add values for any new search checkboxes above this point.
            MustBeLastValue
        }

        private enum SearchNameMode { Localized, Internal, Both }
        private SearchNameMode searchNameMode = SearchNameMode.Both;

        // Initialize both the current and previous states to searching by page name only.
        public PageListSearch() => checkboxValues[0] = previousCheckboxValues[0] = true;

        /// <summary>
        /// Draws the search header for the page list dropdown.
        /// </summary>
        /// <param name="updatePageList">The action to perform if the user updates any of the search parameters.</param>
        public void Build(ImGui gui, Action updatePageList) {
            using (gui.EnterGroup(new Padding(1f))) {
                if (gui.BuildSearchBox(query, out query)) {
                    updatePageList();
                }
                gui.SetTextInputFocus(gui.lastContentRect, query.query);

                gui.BuildText("Search in:");
                using (gui.EnterRow()) {
                    buildCheckbox(gui, "Page name", ref checkboxValues[(int)PageSearchOption.PageName]);
                    buildCheckbox(gui, "Desired products", ref checkboxValues[(int)PageSearchOption.DesiredProducts]);
                    buildCheckbox(gui, "Recipes", ref checkboxValues[(int)PageSearchOption.Recipes]);
                }
                using (gui.EnterRow()) {
                    buildCheckbox(gui, "Ingredients", ref checkboxValues[(int)PageSearchOption.Ingredients]);
                    buildCheckbox(gui, "Extra products", ref checkboxValues[(int)PageSearchOption.ExtraProducts]);
                    if (gui.BuildCheckBox("All", checkboxValues.All(x => x), out bool checkAll)) {
                        if (checkAll) {
                            // Save the previous state, so we can restore it if necessary.
                            Array.Copy(checkboxValues, previousCheckboxValues, (int)PageSearchOption.MustBeLastValue);
                            Array.Fill(checkboxValues, true);
                        }
                        else {
                            // Restore the previous state.
                            Array.Copy(previousCheckboxValues, checkboxValues, (int)PageSearchOption.MustBeLastValue);
                        }
                        updatePageList();
                    }
                }
                using (gui.EnterRow()) {
                    buildRadioButton(gui, "Localized names", SearchNameMode.Localized);
                    buildRadioButton(gui, "Internal names", SearchNameMode.Internal);
                    buildRadioButton(gui, "Both", SearchNameMode.Both);
                }
            }

            void buildCheckbox(ImGui gui, string text, ref bool isChecked) {
                if (gui.BuildCheckBox(text, isChecked, out isChecked)) {
                    updatePageList();
                }
            }

            void buildRadioButton(ImGui gui, string text, SearchNameMode thisValue) {
                // All checkboxes except PageSearchOption.PageName search object names.
                bool isObjectNameSearching = checkboxValues[1..].Any(x => x);
                if (gui.BuildRadioButton(text, searchNameMode == thisValue, enabled: isObjectNameSearching)) {
                    searchNameMode = thisValue;
                    updatePageList();
                }
            }
        }

        /// <summary>
        /// Searches a list of <see cref="ProjectPage"/>s to find the ones that satisfy the current search criteria.
        /// This is typically called by the <c>updatePageList</c> parameter to <see cref="Build"/>.
        /// </summary>
        /// <param name="pages">The <see cref="ProjectPage"/>s to search.</param>
        /// <returns>The <see cref="ProjectPage"/>s that match the current search text and options.</returns>
        public IEnumerable<ProjectPage> Search(IEnumerable<ProjectPage> pages) {
            foreach (var page in pages) {
                if (checkboxValues[(int)PageSearchOption.PageName] && query.Match(page.name)) {
                    yield return page;
                }
                else if (page.content is ProductionTable table) {
                    if (checkboxValues[(int)PageSearchOption.DesiredProducts] && table.links.Any(l => l.amount != 0 && isMatch(l.goods.name, l.goods.locName))) {
                        yield return page;
                    }
                    else if (checkboxValues[(int)PageSearchOption.Ingredients] && table.flow.Any(f => f.amount < 0 && isMatch(f.goods.name, f.goods.locName))) {
                        yield return page;
                    }
                    else if (checkboxValues[(int)PageSearchOption.ExtraProducts] && table.flow.Any(f => f.amount > 0 && isMatch(f.goods.name, f.goods.locName))) {
                        yield return page;
                    }
                    else if (checkboxValues[(int)PageSearchOption.Recipes] && table.GetAllRecipes().Any(r => isMatch(r.recipe.name, r.recipe.locName))) {
                        yield return page;
                    }
                }
            }

            bool isMatch(string internalName, string localizedName)
                => (searchNameMode != SearchNameMode.Internal && query.Match(localizedName)) || (searchNameMode != SearchNameMode.Localized && query.Match(internalName));
        }
    }
}
