using System;

namespace YAFC.UI
{
    public class ExceptionScreen : Window
    {
        private static bool ignore;

        public static void ShowException(Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine(ex.StackTrace);
            if (!ignore)
                new ExceptionScreen(ex);
        }

        public override SchemeColor boxColor => SchemeColor.Error;

        private readonly FontString header;
        private readonly FontString stackTrace;
        public ExceptionScreen(Exception ex)
        {
            ignore = true;
            header = new FontString(Font.header, ex.GetType().Name, color:SchemeColor.ErrorText);
            stackTrace = new FontString(Font.text, ex.StackTrace, true, color:SchemeColor.ErrorText);
            Create(ex.Message, 80, true, null);
        }

        protected override void BuildContent(LayoutState state)
        {
            state.Build(header).Build(stackTrace);
        }
    }
}