using System.Numerics;
using Yafc.Model;
using Yafc.UI;

namespace Yafc {
    public class MilestonesWidget : VirtualScrollList<FactorioObject> {
        public MilestonesWidget() : base(30f, new Vector2(3f, 3f), MilestoneDrawer) => data = Project.current.settings.milestones;

        private static void MilestoneDrawer(ImGui gui, FactorioObject element, int index) {
            var settings = Project.current.settings;
            bool unlocked = settings.Flags(element).HasFlags(ProjectPerItemFlags.MilestoneUnlocked);
            if (gui.BuildFactorioObjectButton(element, 3f, display: MilestoneDisplay.None, bgColor: unlocked ? SchemeColor.Primary : SchemeColor.None) == Click.Left) {
                if (!unlocked) {
                    var massUnlock = Milestones.Instance.GetMilestoneResult(element);
                    int subIndex = 0;
                    settings.SetFlag(element, ProjectPerItemFlags.MilestoneUnlocked, true);
                    foreach (var milestone in settings.milestones) {
                        subIndex++;
                        if (massUnlock[subIndex]) {
                            settings.SetFlag(milestone, ProjectPerItemFlags.MilestoneUnlocked, true);
                        }
                    }
                }
                else {
                    settings.SetFlag(element, ProjectPerItemFlags.MilestoneUnlocked, false);
                }
            }
            if (unlocked && gui.isBuilding) {
                gui.DrawIcon(gui.lastRect, Icon.Check, SchemeColor.Error);
            }
        }
    }

    public class MilestonesPanel : PseudoScreen {
        private static MilestonesWidget? instance;
        private readonly MilestonesWidget milestonesWidget = new();

        public override void Build(ImGui gui) {
            instance = milestonesWidget;
            gui.spacing = 1f;
            BuildHeader(gui, "Milestones");
            gui.BuildText("Please select objects that you already have access to:");
            gui.AllocateSpacing(2f);
            milestonesWidget.Build(gui);
            gui.AllocateSpacing(2f);
            gui.BuildText("For your convenience, YAFC will show objects you DON'T have access to based on this selection", wrap: true);
            gui.BuildText("These are called 'Milestones'. By default all science packs are added as milestones, but this does not have to be this way! " +
                          "You can define your own milestones: Any item, recipe, entity or technology may be added as a milestone. For example you can add advanced " +
                          "electronic circuits as a milestone, and YAFC will display everything that is locked behind those circuits", wrap: true);
            using (gui.EnterRow()) {
                if (gui.BuildButton("Edit milestones", SchemeColor.Grey)) {
                    MilestonesEditor.Show();
                }

                if (gui.RemainingRow().BuildButton("Done")) {
                    Close();
                }
            }
        }

        protected override void ReturnPressed() => Close();

        /// <summary>
        /// Called by <see cref="MilestonesEditor.Close(bool)"/> to force this panel to update its contents and show the possibly-modified milestones.
        /// </summary>
        internal static new void Rebuild() => instance?.RebuildContents();
    }
}
