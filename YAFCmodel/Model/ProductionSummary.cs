using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using YAFC.UI;

namespace YAFC.Model
{
    public class ProductionSummaryEntry : ModelObject<ProductionSummary>
    {
        public ProductionSummaryEntry(ProductionSummary owner, PageReference page) : base(owner)
        {
            this.page = page ?? throw new ArgumentNullException(nameof(page), "Page reference does not exist");
        }

        public float multiplier { get; set; } = 1;
        public PageReference page { get; }

        [SkipSerialization] public Dictionary<Goods, float> flow { get; } = new Dictionary<Goods, float>();
        private bool needRefreshFlow = true;

        public Icon icon
        {
            get
            {
                if (page.page == null)
                    return Icon.Warning;
                return page.page.icon?.icon ?? Icon.None;
            }
        }

        public string name
        {
            get
            {
                if (page != null)
                    return page.page?.name ?? "Page missing";
                return "Broken entry";
            }
        }

        public Task SolveIfNessessary()
        {
            if (page == null)
                return null;
            var solutionPagepage = page.page;
            if (solutionPagepage != null && solutionPagepage.IsSolutionStale())
            {
                needRefreshFlow = true;
                return solutionPagepage.ExternalSolve();
            }
            return null;
        }

        public void RefreshFlow()
        {
            if (!needRefreshFlow)
                return;
            flow.Clear();
            var spage = page?.page?.content as ProductionTable;
            if (spage == null)
                return;

            foreach (var flowEntry in spage.flow)
                flow[flowEntry.goods] = flowEntry.amount * multiplier;
        }
    }

    public class ProductionSummaryColumn : ModelObject<ProductionSummary>
    {
        public ProductionSummaryColumn(ProductionSummary owner, Goods goods) : base(owner)
        {
            this.goods = goods ?? throw new ArgumentNullException(nameof(goods), "Object does not exist");
        }
        public Goods goods { get; }
    }
    
    public class ProductionSummary : ProjectPageContents, IComparer<(Goods goods, float amount)>
    {
        public ProductionSummary(ModelObject page) : base(page) {}
        public List<ProductionSummaryEntry> list { get; } = new List<ProductionSummaryEntry>();
        public List<ProductionSummaryColumn> columns { get; } = new List<ProductionSummaryColumn>();
        [SkipSerialization] public List<(Goods goods, float amount)> nonCapturedFlow { get; } = new List<(Goods goods, float amount)>();

        private Dictionary<Goods, float> totalFlow;

        public override async Task<string> Solve(ProjectPage page)
        {
            var taskList = new List<Task>();
            foreach (var element in list)
            {
                var solutionTask = element.SolveIfNessessary();
                if (solutionTask != null)
                    taskList.Add(solutionTask);
            }

            if (taskList.Count > 0)
                await Task.WhenAll(taskList);
            foreach (var element in list)
                element.RefreshFlow();
            totalFlow = new Dictionary<Goods, float>();
            foreach (var row in list)
            {
                foreach (var (item, amount) in row.flow)
                {
                    totalFlow.TryGetValue(item, out var prev);
                    totalFlow[item] = prev + amount;
                }
            }

            foreach (var column in columns)
                totalFlow.Remove(column.goods);
            nonCapturedFlow.Clear();
            foreach (var element in totalFlow)
                nonCapturedFlow.Add((element.Key, element.Value));
            nonCapturedFlow.Sort(this);
            return null;
        }

        public int Compare((Goods goods, float amount) x, (Goods goods, float amount) y)
        {
            var amt1 = x.goods.fluid != null ? x.amount / 50f : x.amount;
            var amt2 = y.goods.fluid != null ? y.amount / 50f : y.amount;
            return amt1.CompareTo(amt2);
        }
    }
}