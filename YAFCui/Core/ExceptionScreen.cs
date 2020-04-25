using System;
using System.Reflection;

namespace YAFC.UI
{
    public class ExceptionScreen : WindowUtility
    {
        private static bool exists;
        private static bool ignoreAll;

        public static void ShowException(Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine(ex.StackTrace);
            if (!exists && !ignoreAll)
                new ExceptionScreen(ex);
        }

        public override SchemeColor backgroundColor => SchemeColor.Error;
        private Exception ex;
        public ExceptionScreen(Exception ex) : base(new Padding(1f))
        {
            if (ex is TargetInvocationException targetInvocationException)
                ex = targetInvocationException.InnerException;
            this.ex = ex;
            rootGui.initialTextColor = SchemeColor.ErrorText;
            exists = true;
            Create(ex.Message, 80, null);
        }

        protected internal override void Close()
        {
            base.Close();
            exists = false;
        }

        public override void Build(ImGui gui)
        {
            gui.BuildText(ex.GetType().Name, Font.header);
            gui.BuildText(ex.Message, Font.subheader, true);
            gui.BuildText(ex.StackTrace, Font.text, true);
            using (gui.EnterRow(0.5f, RectAllocator.RightRow))
            {
                if (gui.BuildButton("Close"))
                    Close();
                if (gui.BuildButton("Ignore future errors", SchemeColor.Grey))
                {
                    ignoreAll = true;
                    Close();
                }
            }
        }
    }
}