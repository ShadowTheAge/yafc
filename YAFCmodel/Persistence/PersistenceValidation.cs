using System;
using System.Collections.Generic;
using System.Linq;

namespace FactorioData
{
    public enum ValidationErrorSeverity
    {
        Nothing,
        Hint,
        MinorDataLoss,
        MajorDataLoss,
        DataCorruption,
        CriticalIncompatibility
    }
    
    public interface IDataValidator
    {
        void ReportError(ValidationErrorSeverity severity, string message);
    }

    public class DataValidator : IDataValidator
    {
        private readonly Dictionary<string, (ValidationErrorSeverity severity, int count)> errors = new Dictionary<string, (ValidationErrorSeverity, int)>();
        public ValidationErrorSeverity highestError;
        public void ReportError(ValidationErrorSeverity severity, string message)
        {
            if (!errors.TryGetValue(message, out var prev))
                errors[message] = (severity, 1);
            else errors[message] = (prev.severity > severity ? prev.severity : severity, prev.count + 1);
            if (highestError < severity)
                highestError = severity;
        }

        public string ConcatAllErrors()
        {
            return string.Join("\n", errors.OrderByDescending(x => x.Value.severity).ThenByDescending(x => x.Value.count).Select(x => x.Key + (x.Value.count > 1 ? " (x"+x.Value.count+")" : null)));
        }
    }

    public class DeserializationFailedException : Exception
    {
        public DeserializationFailedException(string message) : base(message) {}
    }
}