using Yafc.Model;
using Yafc.UI;

namespace Yafc;

public class ErrorListPanel : PseudoScreen {
    private readonly ErrorCollector collector;
    private readonly ScrollArea verticalList;
    private readonly (string error, ErrorSeverity severity)[] errors;

    private ErrorListPanel(ErrorCollector collector) : base(60f) {
        verticalList = new ScrollArea(30f, BuildErrorList, default, true);
        this.collector = collector;
        errors = collector.GetArrErrors();
    }

    private void BuildErrorList(ImGui gui) {
        foreach (var error in errors) {
            gui.BuildText(error.error, TextBlockDisplayStyle.WrappedText with { Color = error.severity >= ErrorSeverity.MajorDataLoss ? SchemeColor.Error : SchemeColor.BackgroundText });
        }
    }

    public static void Show(ErrorCollector collector) => _ = MainScreen.Instance.ShowPseudoScreen(new ErrorListPanel(collector));
    public override void Build(ImGui gui) {
        if (collector.severity == ErrorSeverity.Critical) {
            BuildHeader(gui, "Loading failed");
        }
        else if (collector.severity >= ErrorSeverity.MinorDataLoss) {
            BuildHeader(gui, "Loading completed with errors");
        }
        else {
            BuildHeader(gui, "Analysis warnings");
        }

        verticalList.Build(gui);

    }

    protected override void ReturnPressed() => Close();
}
