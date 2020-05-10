using System;
using YAFC.Model;
using YAFC.UI;

namespace YAFC
{
    public class AutoPlannerView : ProjectPageView<AutoPlanner>
    {
        private readonly WizardPanel AutoPlannerWizard;

        public AutoPlannerView()
        {
            AutoPlannerWizard = new WizardPanel("Create auto planner", AutoPlannerWizardFinish, AutoPlannerWizardPage1);
        }

        private void AutoPlannerWizardPage1(ImGui imGui, ref bool valid)
        {
            
        }

        private void AutoPlannerWizardFinish()
        {
            
        }

        public override void BuildHeader(ImGui gui)
        {
            
        }

        public override void BuildContent(ImGui gui)
        {
            
        }

        public override void CreateModelDropdown(ImGui gui, Type type, Project project, ref bool close)
        {
            if (gui.BuildButton("Auto planner"))
            {
                close = true;
                AutoPlannerWizard.Show();
            }
        }
    }
}