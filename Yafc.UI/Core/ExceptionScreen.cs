using System;
using SDL2;
using Serilog;

namespace Yafc.UI;

public class ExceptionScreen : WindowUtility {
    private static readonly ILogger logger = Logging.GetLogger<ExceptionScreen>();
    private static bool exists;
    private static bool ignoreAll;

    public static void ShowException(Exception ex) {
        logger.Error(ex, "Exception encountered");

        if (!exists && !ignoreAll) {
            exists = true;
            Ui.DispatchInMainThread(state => new ExceptionScreen(ex), null);
        }
    }

    public override SchemeColor backgroundColor => SchemeColor.Error;
    private readonly Exception ex;
    public ExceptionScreen(Exception ex) : base(new Padding(1f)) {
        while (ex.InnerException != null) {
            ex = ex.InnerException;
        }

        this.ex = ex;
        rootGui.initialTextColor = SchemeColor.ErrorText;
        Create(ex.Message, 80, null);
    }

    protected internal override void Close() {
        base.Close();
        exists = false;
    }

    protected override void BuildContents(ImGui gui) {
        gui.BuildText(ex.GetType().Name, Font.header);
        gui.BuildText(ex.Message, new TextBlockDisplayStyle(Font.subheader, true));
        gui.BuildText(ex.StackTrace, TextBlockDisplayStyle.WrappedText);

        using (gui.EnterRow(0.5f, RectAllocator.RightRow)) {
            if (gui.BuildButton("Close")) {
                Close();
            }

            if (gui.BuildButton("Ignore future errors", SchemeColor.Grey)) {
                ignoreAll = true;
                Close();
            }
            if (gui.BuildButton("Copy to clipboard", SchemeColor.Grey)) {
                _ = SDL.SDL_SetClipboardText(ex.Message + "\n\n" + ex.StackTrace);
            }
        }
    }
}
