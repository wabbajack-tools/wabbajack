using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wabbajack.Common.StatusFeed.Errors
{
    public class GenericException : IException
    {
        public string ExtraMessage { get; } = string.Empty;

        public DateTime Timestamp { get; } = DateTime.Now;

        public string ShortDescription => string.IsNullOrWhiteSpace(ExtraMessage) ? Exception?.Message ?? string.Empty : $"{ExtraMessage} - {Exception?.Message}";

        public string ExtendedDescription => string.IsNullOrWhiteSpace(ExtraMessage) ? Exception?.ToString() ?? string.Empty : $"{ExtraMessage} - {Exception?.ToString()}";

        public Exception Exception { get; }

        public GenericException(Exception exception, string? extraMessage = null)
        {
            ExtraMessage = extraMessage ?? string.Empty;
            Exception = exception;
        }
    }
}
