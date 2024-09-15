using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Serilog;
using Yafc.UI;

namespace Yafc.Model;
public enum ErrorSeverity {
    None,
    AnalysisWarning,
    MinorDataLoss,
    MajorDataLoss,
    Important,
    Critical
}

public class ErrorCollector {
    private static readonly ILogger logger = Logging.GetLogger<ErrorCollector>();
    private readonly Dictionary<(string message, ErrorSeverity severity), int> allErrors = [];
    public ErrorSeverity severity { get; private set; }
    public void Error(string message, ErrorSeverity severity) {
        var key = (message, severity);
        if (severity > this.severity) {
            this.severity = severity;
        }

        _ = allErrors.TryGetValue(key, out int prevC);
        allErrors[key] = prevC + 1;
        logger.Information(message);
    }

    public (string error, ErrorSeverity severity)[] GetArrErrors() {
        return allErrors.OrderByDescending(x => x.Key.severity).ThenByDescending(x => x.Value).Select(x => (x.Value == 1 ? x.Key.message : x.Key.message + " (x" + x.Value + ")", x.Key.severity)).ToArray();
    }

    public void Exception(Exception exception, string message, ErrorSeverity errorSeverity) {
        while (exception.InnerException != null) {
            exception = exception.InnerException;
        }

        string s = message + ": ";
        if (exception is JsonException) {
            s += "unexpected or invalid json";
        }
        else if (exception is ArgumentNullException argnull) {
            s += argnull.Message;
        }
        else if (exception is NotSupportedException notSupportedException) {
            s += notSupportedException.Message;
        }
        else if (exception is InvalidOperationException unexpectedNull) {
            s += unexpectedNull.Message;
        }
        else {
            s += exception.GetType().Name;
        }

        Error(s, errorSeverity);
        logger.Error(exception, "Exception encountered");
    }
}
