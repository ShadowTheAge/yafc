using System.Linq;
using System.Numerics;
using Yafc.Model;
using Yafc.UI;

namespace Yafc {
    public class MilestonesEditor : PseudoScreen {
        private static readonly MilestonesEditor Instance = new MilestonesEditor();
        private readonly VirtualScrollList<FactorioObject> milestoneList;

        public MilestonesEditor() {
            milestoneList = new VirtualScrollList<FactorioObject>(30f, new Vector2(float.PositiveInfinity, 3f), MilestoneDrawer);
        }

        public override void Open() {
            base.Open();
            milestoneList.data = Project.current.settings.milestones;
        }

        public static void Show() => _ = MainScreen.Instance.ShowPseudoScreen(Instance);

        private void MilestoneDrawer(ImGui gui, FactorioObject element, int index) {
            using (gui.EnterRow()) {
                var settings = Project.current.settings;
                gui.BuildFactorioObjectIcon(element, MilestoneDisplay.None, 3f);
                gui.BuildText(element.locName);
                if (gui.BuildButton(Icon.Close, size: 1f)) {
                    _ = settings.RecordUndo().milestones.Remove(element);
                    Rebuild();
                    milestoneList.data = settings.milestones;
                }
            }
            if (gui.DoListReordering(gui.lastRect, gui.lastRect, index, out int moveFrom)) {
                Project.current.settings.RecordUndo().milestones.MoveListElementIndex(moveFrom, index);
            }
        }

        public override void Build(ImGui gui) {
            BuildHeader(gui, "Milestone editor");
            milestoneList.Build(gui);
            gui.BuildText(
                "Hint: You can reorder milestones. When an object is locked behind a milestone, the first inaccessible milestone will be shown. Also when there is a choice between different milestones, first will be chosen",
                wrap: true, color: SchemeColor.BackgroundTextFaint);
            using (gui.EnterRow()) {
                if (gui.BuildButton("Auto sort milestones", SchemeColor.Grey)) {
                    ErrorCollector collector = new ErrorCollector();
                    Milestones.Instance.ComputeWithParameters(Project.current, collector, Project.current.settings.milestones.ToArray(), true);
                    if (collector.severity > ErrorSeverity.None) {
                        ErrorListPanel.Show(collector);
                    }

                    milestoneList.RebuildContents();
                }
                if (gui.BuildButton("Add milestone")) {
                    SelectMultiObjectPanel.Select(Database.objects.all.Except(Project.current.settings.milestones), "Add new milestone", AddMilestone);
                }
            }
        }

        private void AddMilestone(FactorioObject obj) {
            var settings = Project.current.settings;
            if (settings.milestones.Contains(obj)) {
                MessageBox.Show(null, "Milestone already exists", "Ok");
                return;
            }
            var lockedMask = Milestones.Instance.GetMilestoneResult(obj);
            if (lockedMask.IsClear()) {
                settings.RecordUndo().milestones.Add(obj);
            }
            else {
                int bestIndex = 0;
                for (int i = 1; i < lockedMask.length; i++) {
                    if (lockedMask[i]) {
                        lockedMask[i] = false;
                        var milestone = Milestones.Instance.currentMilestones[i - 1];
                        int index = settings.milestones.IndexOf(milestone);
                        if (index >= bestIndex) {
                            bestIndex = index + 1;
                        }

                        if (lockedMask.IsClear()) {
                            break;
                        }
                    }
                }
                settings.RecordUndo().milestones.Insert(bestIndex, obj);
            }
            Rebuild();
            milestoneList.data = settings.milestones;
        }
    }
}
