using System;
using YAFC.Model;
using YAFC.UI;

namespace YAFC
{
    public class LoadingErrorsScreen : WindowUtility
    {
        public LoadingErrorsScreen(Exception ex) : base(ImGuiUtils.DefaultScreenPadding) {}
        public LoadingErrorsScreen(ErrorCollector errors) : base(ImGuiUtils.DefaultScreenPadding) {}

        protected override void BuildContents(ImGui gui)
        {
            
        }
    }
}