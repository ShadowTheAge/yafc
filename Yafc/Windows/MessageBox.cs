using System;
using System.Threading.Tasks;
using Yafc.UI;

namespace Yafc {
    public class MessageBox : PseudoScreen<bool> {
        private readonly string title;
        private readonly string message;
        private readonly string yes;
        private readonly string? no;

        private MessageBox(string title, string message, string yes, string? no) : base(30f) {
            this.title = title;
            this.message = message;
            this.yes = yes;
            this.no = no;
        }

        public static void Show(Action<bool, bool>? result, string title, string message, string yes, string? no) {
            MessageBox instance = new MessageBox(title, message, yes, no) { complete = result };
            _ = MainScreen.Instance.ShowPseudoScreen(instance);
        }

        public static void Show(string title, string message, string yes) => Show(null, title, message, yes, null);

        public static Task<(bool haveChoice, bool choice)> Show(string title, string message, string yes, string? no) {
            TaskCompletionSource<(bool, bool)> tcs = new TaskCompletionSource<(bool, bool)>();
            Show((a, b) => tcs.TrySetResult((a, b)), title, message, yes, no);
            return tcs.Task;
        }

        public override void Build(ImGui gui) {
            BuildHeader(gui, title);
            if (message != null) {
                gui.BuildText(message, wrap: true);
            }

            gui.AllocateSpacing(2f);
            using (gui.EnterRow(allocator: RectAllocator.RightRow)) {
                if (gui.BuildButton(yes)) {
                    CloseWithResult(true);
                }

                if (no != null && gui.BuildButton(no, SchemeColor.Grey)) {
                    CloseWithResult(false);
                }
            }
        }

        protected override void ReturnPressed() => CloseWithResult(true);
    }
}
