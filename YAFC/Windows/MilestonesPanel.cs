using YAFC.Model;
using YAFC.UI;

namespace YAFC
{
    public class MilestonesWidget
    {
        public static readonly MilestonesWidget Instance = new MilestonesWidget();
        public void Build(ImGui gui)
        {
            var settings = MainScreen.Instance.project.settings;
            using (var grid = gui.EnterInlineGrid(3f))
            {
                foreach (var cur in settings.milestones)
                {
                    grid.Next();
                    var unlocked = settings.Flags(cur).HasFlags(ProjectPerItemFlags.MilestoneUnlocked);
                    if (gui.BuildFactorioObjectButton(cur, 3f, MilestoneDisplay.None, unlocked ? SchemeColor.Primary : SchemeColor.None))
                    {
                        if (!unlocked)
                        {
                            var massUnlock = Milestones.milestoneResult[cur.id];
                            var subIndex = 0;
                            settings.SetFlag(cur, ProjectPerItemFlags.MilestoneUnlocked, true);
                            foreach (var milestone in settings.milestones)
                            {
                                subIndex++;
                                if ((massUnlock & (1ul << subIndex)) != 0)
                                    settings.SetFlag(milestone, ProjectPerItemFlags.MilestoneUnlocked, true);
                            }
                        }
                        else
                        {
                            settings.SetFlag(cur, ProjectPerItemFlags.MilestoneUnlocked, false);
                        }
                    }
                    if (unlocked && gui.isBuilding)
                        gui.DrawIcon(gui.lastRect, Icon.Check, SchemeColor.Error);
                }
            }
        }
    }
    
    public class MilestonesPanel : PseudoScreen
    {
        public static readonly MilestonesPanel Instance = new MilestonesPanel();

        public override void Build(ImGui gui)
        {
            gui.spacing = 1f;
            BuildHeader(gui, "Milestones");
            gui.BuildText("Please select objects that you already have access to:");
            gui.AllocateSpacing(2f);
            MilestonesWidget.Instance.Build(gui);
            gui.AllocateSpacing(2f);
            gui.BuildText("For your convinience, YAFC will show objects you DON'T have access to based on this selection", wrap:true);
            gui.BuildText("These are called 'Milestones'. By default all science packs are added as milestones, but this does not have to be this way! " +
                          "You can define your own milestones: Any item, recipe, entity or technology may be added as a milestone. For example you can add advanced " +
                          "electronic circuits as a milestone, and YAFC will display everything that is locked behind those circuits", wrap: true);
            using (gui.EnterRow())
            {
                if (gui.BuildButton("Edit milestones", SchemeColor.Grey))
                {
                    
                }
                if (gui.RemainingRow().BuildButton("Done"))
                    Close();
            }
        }
    }
}