using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wabbajack.Common.StatusFeed.Errors
{
    public class GenericException : IException
    {
        public string ExtraMessage { get; }

        public DateTime Timestamp { get; } = DateTime.Now;

        public string ShortDescription => ExtraMessage == null ? Exception?.Message : $"{ExtraMessage} - {Exception?.Message}";

        public string ExtendedDescription => ExtraMessage == null ? Exception?.ToString() : $"{ExtraMessage} - {Exception?.ToString()}";

        public Exception Exception { get; }

        public GenericException(Exception exception, string extraMessage = null)
        {
            ExtraMessage = extraMessage;
            Exception = exception;
        }
    }
}
