using System;
using YAFC.Model;
using YAFC.UI;

namespace YAFC
{
    public class ProductionSummaryView : ProjectPageView<ProductionSummary>
    {
        private DataGrid<ProductionSummaryEntry> grid;
        private readonly VirtualScrollList<ProjectPage> pagesDropdown;

        public ProductionSummaryView()
        {
            //grid = new DataGrid<ProductionSummaryEntry>(new DataColumn<ProductionSummaryEntry>("Production block", BuildProductionBlockEntry));
        }

        private void BuildProductionBlockEntry(ImGui gui, ProductionSummaryEntry entry)
        {
        }

        protected override void BuildContent(ImGui gui)
        {
            if (model == null)
                return;
            return;
            BuildSummary(gui);
            gui.AllocateSpacing();
            var hasReorder = grid.BuildContent(gui, model.list, out var reorder, out var rect);
            gui.SetMinWidth(grid.width);
            if (gui.BuildButton(Icon.Add))
            {
                
            }
        }

        private void BuildSummary(ImGui gui)
        {
            
        }

        public override void CreateModelDropdown(ImGui gui, Type type, Project project)
        {
            if (gui.BuildContextMenuButton("Create production summary") && gui.CloseDropdown())
                ProjectPageSettingsPanel.Show(null, (name, icon) => MainScreen.Instance.AddProjectPage(name, icon, typeof(ProductionSummary), true));
        }

        protected override void BuildPageTooltip(ImGui gui, ProductionSummary contents)
        {
            
        }
    }
}