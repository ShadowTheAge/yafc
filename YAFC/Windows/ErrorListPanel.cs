using YAFC.Model;
using YAFC.UI;

namespace YAFC
{
    public class ErrorListPanel : PseudoScreen
    {
        private static readonly ErrorListPanel Instance = new ErrorListPanel();
        private ErrorCollector collector;
        private readonly VerticalScrollCustom verticalList;
        private (string error, ErrorSeverity severity)[] errors;

        public ErrorListPanel() : base(60f)
        {
            verticalList = new VerticalScrollCustom(30f, BuildErrorList, default, true);
        }

        private void BuildErrorList(ImGui gui)
        {
            foreach (var error in errors)
            {
                gui.BuildText(error.error, wrap:true, color:error.severity >= ErrorSeverity.MajorDataLoss ? SchemeColor.Error : SchemeColor.BackgroundText);
            }
        }

        public static void Show(ErrorCollector collector)
        {
            Instance.collector = collector;
            Instance.errors = collector.GetArrErrors();
            MainScreen.Instance.ShowPseudoScreen(Instance);
        }
        public override void Build(ImGui gui)
        {
            if (collector.severity == ErrorSeverity.Critical)
                BuildHeader(gui, "Loading failed");
            else if (collector.severity >= ErrorSeverity.MinorDataLoss)
                BuildHeader(gui, "Loading completed with errors");
            else BuildHeader(gui, "Analysis warnings");
            verticalList.Build(gui);
                
        }
    }
}