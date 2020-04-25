using System;
using System.Reflection;

namespace YAFC.UI
{
    public class ExceptionScreen : WindowUtility
    {
        private static bool ignore;

        public static void ShowException(Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine(ex.StackTrace);
            if (!ignore)
                new ExceptionScreen(ex);
        }

        public override SchemeColor backgroundColor => SchemeColor.Error;
        private Exception ex;
        public ExceptionScreen(Exception ex)
        {
            if (ex is TargetInvocationException targetInvocationException)
                ex = targetInvocationException.InnerException;
            this.ex = ex;
            ignore = true;
            Create(ex.Message, 80, null);
        }

        public override void Build(ImGui gui)
        {
            gui.BuildText(ex.GetType().Name, Font.header);
            gui.BuildText(ex.Message, Font.subheader, true);
            gui.BuildText(ex.StackTrace, Font.text, true);
        }
    }
}