using System;
using System.Collections.Generic;
using YAFC.Model;
using YAFC.UI;

namespace YAFC
{
    public class ProductionSummaryFlatHierarchy : FlatHierarchy<ProductionSummaryEntry, ProductionSummaryGroup>
    {
        public ProductionSummaryFlatHierarchy(DataGrid<ProductionSummaryEntry> grid, Action<ImGui, ProductionSummaryGroup> drawTableHeader) : base(grid, drawTableHeader) {}
        protected override bool Expanded(ProductionSummaryGroup group) => group.expanded;
        protected override ProductionSummaryGroup Subgroup(ProductionSummaryEntry row) => row.group;
        protected override List<ProductionSummaryEntry> Elements(ProductionSummaryGroup @group) => group.list;
        protected override void SetOwner(ProductionSummaryEntry row, ProductionSummaryGroup newOwner) => row.SetOwner(newOwner);
        protected override bool Filter(ProductionSummaryEntry row) => row.filterMatch;
        protected override string emptyGroupMessage => "This is an empty group";
    }
}