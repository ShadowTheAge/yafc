using YAFC.Model;
using YAFC.Parser;
using YAFC.UI;

namespace YAFC
{
    public class MilestonesPanel : PseudoScreen
    {
        public static readonly MilestonesPanel Instance = new MilestonesPanel();
        private ulong milestoneMask;
        public override void Open()
        {
            milestoneMask = MainScreen.Instance.project.settings.milestonesUnlockedMask;
            base.Open();
        }

        public override void Build(ImGui gui)
        {
            gui.spacing = 1f;
            BuildHeader(gui, "Milestones");
            gui.BuildText("Please select objects that you already have access to:");
            gui.AllocateSpacing(2f);
            var index = 0;
            foreach (var cur in gui.BuildInlineGrid(MainScreen.Instance.project.settings.milestones, 3f))
            {
                var bit = 1ul << index++;
                var unlocked = (milestoneMask & bit) != 0;
                if (gui.BuildFactorioObjectButton(cur, 3f, MilestoneDisplay.None, unlocked ? SchemeColor.Primary : SchemeColor.None))
                {
                    if (!unlocked)
                    {
                        var massUnlock = Milestones.milestoneResult[cur.id] >> 1;
                        milestoneMask |= (massUnlock | bit);
                    }
                    else
                    {
                        milestoneMask &= ~bit;
                    }
                }
                if (unlocked && gui.action == ImGuiAction.Build)
                    gui.DrawIcon(gui.lastRect, Icon.Check, SchemeColor.Source);
            }
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

        protected override void Save()
        {
            var psettings = MainScreen.Instance.project.settings;
            if (milestoneMask != psettings.milestonesUnlockedMask)
            {
                psettings.RecordUndo().milestonesUnlockedMask = milestoneMask;
                Milestones.SetUnlockedMask(milestoneMask);
            }
            base.Save();
        }
    }
}