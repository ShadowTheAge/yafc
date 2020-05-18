using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;

namespace YAFC.Model
{
    public enum ErrorSeverity
    {
        None,
        Warning,
        MinorDataLoss,
        MajorDataLoss,
        SuperImportant,
    }
    
    public class ErrorCollector
    {
        private Dictionary<string, int> allErrors;
        public ErrorSeverity severity {get; private set; }
        public void Error(string message, ErrorSeverity severity)
        {
            if (allErrors == null)
                allErrors = new Dictionary<string, int>();
            if (severity > this.severity)
                this.severity = severity;
            allErrors.TryGetValue(message, out var prevC);
            allErrors[message] = prevC + 1;
            Console.WriteLine(message);
        }

        public void Exception(Exception exception, string message, ErrorSeverity errorSeverity)
        {
            while (exception.InnerException != null)
                exception = exception.InnerException; 
            var s = message+": ";
            if (exception is JsonException)
                s += "unexpected or invalid json";
            else if (exception is ArgumentNullException argnull)
                s += argnull.Message;
            else s += exception.GetType().Name;
            Error(s, errorSeverity);
            Console.Error.WriteLine(exception.StackTrace);
        }
    }
}