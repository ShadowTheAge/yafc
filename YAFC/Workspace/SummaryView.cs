using System;
using YAFC.Model;
using YAFC.UI;

namespace YAFC {
    public class SummaryView : ProjectPageView<Summary> {
        public override void SetModel(ProjectPage page) {
            base.SetModel(page);
        }
        protected override void BuildPageTooltip(ImGui gui, Summary contents) {
        }

        protected override void BuildContent(ImGui gui) {

        }

        public override void CreateModelDropdown(ImGui gui, Type type, Project project) {
        }
    }
}