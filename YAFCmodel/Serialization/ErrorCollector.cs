using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace YAFC.Model {
    public enum ErrorSeverity {
        None,
        AnalysisWarning,
        MinorDataLoss,
        MajorDataLoss,
        Important,
        Critical
    }

    public class ErrorCollector {
        private Dictionary<(string message, ErrorSeverity severity), int> allErrors;
        public ErrorSeverity severity { get; private set; }
        public void Error(string message, ErrorSeverity severity) {
            var key = (message, severity);
            if (allErrors == null)
                allErrors = new Dictionary<(string, ErrorSeverity), int>();
            if (severity > this.severity)
                this.severity = severity;
            allErrors.TryGetValue(key, out var prevC);
            allErrors[key] = prevC + 1;
            Console.WriteLine(message);
        }

        public (string error, ErrorSeverity severity)[] GetArrErrors() {
            return allErrors.OrderByDescending(x => x.Key.severity).ThenByDescending(x => x.Value).Select(x => (x.Value == 1 ? x.Key.message : x.Key.message + " (x" + x.Value + ")", x.Key.severity)).ToArray();
        }

        public void Exception(Exception exception, string message, ErrorSeverity errorSeverity) {
            while (exception.InnerException != null)
                exception = exception.InnerException;
            var s = message + ": ";
            if (exception is JsonException)
                s += "unexpected or invalid json";
            else if (exception is ArgumentNullException argnull)
                s += argnull.Message;
            else if (exception is NotSupportedException notSupportedException)
                s += notSupportedException.Message;
            else s += exception.GetType().Name;
            Error(s, errorSeverity);
            Console.Error.WriteLine(exception.StackTrace);
        }
    }
}