using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Yafc.UI;

namespace Yafc.Model {
    public class ProductionSummaryGroup(ModelObject owner) : ModelObject<ModelObject>(owner), IElementGroup<ProductionSummaryEntry> {
        public List<ProductionSummaryEntry> elements { get; } = [];
        [NoUndo]
        public bool expanded { get; set; }
        public string? name { get; set; }

        public void Solve(Dictionary<Goods, float> totalFlow, float multiplier) {
            foreach (var element in elements) {
                element.RefreshFlow();
            }

            totalFlow.Clear();
            foreach (var row in elements) {
                foreach (var (item, amount) in row.flow) {
                    _ = totalFlow.TryGetValue(item, out float prev);
                    totalFlow[item] = prev + (amount * multiplier);
                }
            }
        }

        public void UpdateFilter(Goods filteredGoods, SearchQuery searchQuery) {
            foreach (var element in elements) {
                element.UpdateFilter(filteredGoods, searchQuery);
            }
        }
    }

    public class ProductionSummaryEntry(ProductionSummaryGroup owner) : ModelObject<ProductionSummaryGroup>(owner), IGroupedElement<ProductionSummaryGroup> {
        protected internal override void AfterDeserialize() {
            // Must be either page reference, or subgroup, not both
            if (subgroup == null && page == null) {
                throw new NotSupportedException("Referenced page does not exist");
            }

            if (subgroup != null && page != null) {
                page = null;
            }

            base.AfterDeserialize();
        }

        public float multiplier { get; set; } = 1;
        public PageReference? page { get; set; }
        public ProductionSummaryGroup? subgroup { get; set; }
        public bool visible { get; private set; } = true;
        [SkipSerialization] public Dictionary<Goods, float> flow { get; } = [];
        private bool needRefreshFlow = true;

        public Icon icon {
            get {
                if (subgroup != null) {
                    return Icon.Folder;
                }

                if (page?.page == null) {
                    return Icon.Warning;
                }

                return page.page.icon?.icon ?? Icon.None;
            }
        }

        public string name {
            get {
                if (page != null) {
                    return page.page?.name ?? "Page missing";
                }

                return "Broken entry";
            }
        }

        public bool CollectSolvingTasks(List<Task> listToFill) {
            var solutionTask = SolveIfNecessary();
            if (solutionTask != null) {
                listToFill.Add(solutionTask);
                needRefreshFlow = true;
            }

            if (subgroup != null) {
                foreach (var element in subgroup.elements) {
                    needRefreshFlow |= element.CollectSolvingTasks(listToFill);
                }
            }
            return needRefreshFlow;
        }

        public Task? SolveIfNecessary() {
            if (page == null) {
                return null;
            }

            var solutionPagepage = page.page;
            if (solutionPagepage != null && solutionPagepage.IsSolutionStale()) {
                return solutionPagepage.ExternalSolve();
            }

            return null;
        }

        public float GetAmount(Goods goods) {
            return flow.TryGetValue(goods, out float amount) ? amount : 0;
        }

        public void RefreshFlow() {
            if (!needRefreshFlow) {
                return;
            }

            needRefreshFlow = false;
            flow.Clear();
            if (subgroup != null) {
                subgroup.Solve(flow, multiplier);
            }
            else {
                if (page?.page?.content is not ProductionTable subTable) {
                    return;
                }

                foreach (var flowEntry in subTable.flow) {
                    if (flowEntry.amount != 0) {
                        flow[flowEntry.goods] = flowEntry.amount * multiplier;
                    }
                }

                foreach (var link in subTable.links) {
                    if (link.amount != 0) {
                        _ = flow.TryGetValue(link.goods, out float prevValue);
                        flow[link.goods] = prevValue + (link.amount * multiplier);
                    }
                }
            }
        }

        public void SetOwner(ProductionSummaryGroup newOwner) {
            owner = newOwner;
        }

        public void UpdateFilter(Goods goods, SearchQuery query) {
            visible = flow.ContainsKey(goods);
            subgroup?.UpdateFilter(goods, query);
        }

        public void SetMultiplier(float newMultiplier) {
            _ = this.RecordUndo();
            needRefreshFlow = true;
            multiplier = newMultiplier;
        }
    }

    public class ProductionSummaryColumn(ProductionSummary owner, Goods goods) : ModelObject<ProductionSummary>(owner) {
        public Goods goods { get; } = goods ?? throw new ArgumentNullException(nameof(goods), "Object does not exist");
    }

    public class ProductionSummary : ProjectPageContents, IComparer<(Goods goods, float amount)> {
        public ProductionSummary(ModelObject page) : base(page) {
            group = new ProductionSummaryGroup(this);
        }
        public ProductionSummaryGroup group { get; }
        public List<ProductionSummaryColumn> columns { get; } = [];
        [SkipSerialization] public List<(Goods goods, float amount)> sortedFlow { get; } = [];

        private readonly Dictionary<Goods, float> totalFlow = [];
        [SkipSerialization] public HashSet<Goods> columnsExist { get; } = [];

        public override void InitNew() {
            columns.Add(new ProductionSummaryColumn(this, Database.electricity));
            base.InitNew();
        }

        public float GetTotalFlow(Goods goods) {
            return totalFlow.TryGetValue(goods, out float amount) ? amount : 0;
        }

        public override async Task<string?> Solve(ProjectPage page) {
            List<Task> taskList = [];
            foreach (var element in group.elements) {
                _ = element.CollectSolvingTasks(taskList);
            }

            if (taskList.Count > 0) {
                await Task.WhenAll(taskList);
            }

            columnsExist.Clear();
            group.Solve(totalFlow, 1);

            foreach (var column in columns) {
                _ = columnsExist.Add(column.goods);
            }

            sortedFlow.Clear();
            foreach (var element in totalFlow) {
                sortedFlow.Add((element.Key, element.Value));
            }

            sortedFlow.Sort(this);
            return null;
        }

        public int Compare((Goods goods, float amount) x, (Goods goods, float amount) y) {
            float amt1 = x.goods.fluid != null ? x.amount / 50f : x.amount;
            float amt2 = y.goods.fluid != null ? y.amount / 50f : y.amount;
            return amt1.CompareTo(amt2);
        }
    }
}
