using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace YAFC.Model
{
    public interface IProductionSummaryCollection
    {
        public IEnumerable<(Goods goods, float amount)> GetFlow();
    }
    
    public class ProductionSummaryEntry : ModelObject<ProductionSummary>
    {
        public ProductionSummaryEntry(ProductionSummary owner) : base(owner) {}

        public float multiplier { get; set; } = 1;
        public PageReference page { get; set; }
        public Recipe recipe { get; set; }

        public Task SolveIfNessessary()
        {
            var page = this.page.page;
            if (page != null && page.IsSolutionStale())
                return page.ExternalSolve();
            return null;
        }
    }

    public class ProductionSummaryColumn : ModelObject<ProductionSummary>
    {
        public ProductionSummaryColumn(ProductionSummary owner) : base(owner) {}
        public Goods goods;
    }
    
    public class ProductionSummary : ProjectPageContents
    {
        public ProductionSummary(ModelObject page) : base(page) {}
        public List<ProductionSummaryEntry> list { get; } = new List<ProductionSummaryEntry>();
        public List<ProductionSummaryColumn> columns { get; } = new List<ProductionSummaryColumn>();

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
            return null;
        }
    }
}