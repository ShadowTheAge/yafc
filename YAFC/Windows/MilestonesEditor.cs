using System.Numerics;
using YAFC.Model;
using YAFC.UI;

namespace YAFC
{
    public class MilestonesEditor : PseudoScreen
    {
        private static readonly MilestonesEditor Instance = new MilestonesEditor();
        private readonly VirtualScrollList<FactorioObject> milestoneList;

        public MilestonesEditor()
        {
            milestoneList = new VirtualScrollList<FactorioObject>(30f, new Vector2(float.PositiveInfinity, 3f), MilestoneDrawer);
        }

        public override void Open()
        {
            base.Open();
            milestoneList.data = Project.current.settings.milestones;
        }

        public static void Show() => MainScreen.Instance.ShowPseudoScreen(Instance);

        private void MilestoneDrawer(ImGui gui, FactorioObject element, int index)
        {
            using (gui.EnterRow())
            {
                var settings = Project.current.settings;
                gui.BuildFactorioObjectIcon(element, MilestoneDisplay.None, 3f);
                gui.BuildText(element.locName);
                if (gui.BuildButton(Icon.Close, size: 1f))
                {
                    settings.RecordUndo().milestones.Remove(element);
                    Rebuild();
                    milestoneList.data = settings.milestones;
                }
            }
            if (gui.DoListReordering(gui.lastRect, gui.lastRect, index, out var moveFrom))
                Project.current.settings.RecordUndo().milestones.MoveListElementIndex(moveFrom, index);
        }

        public override void Build(ImGui gui)
        {
            BuildHeader(gui, "Milestone editor");
            milestoneList.Build(gui);
            gui.BuildText(
                "Hint: You can reorder milestones. When an object is locked behind a milestone, the first inaccessible milestone will be shown. Also when there is a choice between different milestones, first will be chosen",
                wrap: true, color: SchemeColor.BackgroundTextFaint);
            using (gui.EnterRow())
            {
                if (gui.BuildButton("Auto sort milestones", SchemeColor.Grey))
                {
                    var collector = new ErrorCollector();
                    Milestones.Instance.ComputeWithParameters(Project.current, collector, Project.current.settings.milestones.ToArray(), true);
                    if (collector.severity > ErrorSeverity.None)
                        ErrorListPanel.Show(collector);
                    milestoneList.RebuildContents();
                }
                if (gui.BuildButton("Add milestone"))
                {
                    if (Project.current.settings.milestones.Count >= 60)
                        MessageBox.Show(null, "Milestone limit reached", "60 milestones is the limit. You may delete some of the milestones you've already reached.", "Ok");
                    else SelectObjectPanel.Select(Database.objects.all, "Add new milestone", AddMilestone);
                }
            }
        }

        private void AddMilestone(FactorioObject obj)
        {
            var settings = Project.current.settings;
            if (settings.milestones.Contains(obj))
            {
                MessageBox.Show(null, "Milestone already exists", "Ok");
                return;
            }
            var lockedMask = Milestones.Instance.milestoneResult[obj];
            if (lockedMask == 0)
            {
                settings.RecordUndo().milestones.Add(obj);
            }
            else
            {
                var bestIndex = 0;
                for (var i = 1; i < 64; i++)
                {
                    var mask = (1ul << i);
                    if ((lockedMask & mask) != 0)
                    {
                        lockedMask &= ~mask;
                        var milestone = Milestones.Instance.currentMilestones[i - 1];
                        var index = settings.milestones.IndexOf(milestone);
                        if (index >= bestIndex)
                            bestIndex = index + 1;
                        if (lockedMask == 0)
                            break;
                    }
                }
                settings.RecordUndo().milestones.Insert(bestIndex, obj);
            }
            Rebuild();
            milestoneList.data = settings.milestones;
        }
    }
}