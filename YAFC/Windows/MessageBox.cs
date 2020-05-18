using System;
using YAFC.UI;

namespace YAFC
{
    public class MessageBox : PseudoScreen<bool>
    {
        public MessageBox() : base(30f) {}
        private static readonly MessageBox Instance = new MessageBox();

        private string title;
        private string message;
        private string yes;
        private string no;

        public static void Show(Action<bool, bool> result, string title, string message, string yes, string no = null)
        {
            Instance.title = title;
            Instance.complete = result;
            Instance.message = message;
            Instance.yes = yes;
            Instance.no = no;
            MainScreen.Instance.ShowPseudoScreen(Instance);
        }
        
        public override void Build(ImGui gui)
        {
            BuildHeader(gui, title);
            if (message != null)
                gui.BuildText(message, wrap:true);
            gui.AllocateSpacing(2f);
            using (gui.EnterRow(allocator:RectAllocator.RightRow))
            {
                if (gui.BuildButton(yes))
                    CloseWithResult(true);
                if (no != null && gui.BuildButton(no, SchemeColor.Grey))
                    CloseWithResult(false);
            }
        }
    }
}