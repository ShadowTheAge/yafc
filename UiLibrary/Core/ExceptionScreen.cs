using System;

namespace UI
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

        private FontString header;
        private FontString stackTrace;
        public ExceptionScreen(Exception ex)
        {
            backgroundColor = SchemeColor.Error;
            ignore = true;
            header = new FontString(Font.header, ex.GetType().Name, color:SchemeColor.ErrorText);
            stackTrace = new FontString(Font.text, ex.StackTrace, true, color:SchemeColor.ErrorText);
            Create(ex.Message, 80, true);
        }

        protected override LayoutPosition BuildContent(RenderBatch batch, LayoutPosition location)
        {
            location.Build(header, batch);
            location.Build(stackTrace, batch);
            return location;
        }
    }
}