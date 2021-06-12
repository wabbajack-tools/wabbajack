using System;

namespace Wabbajack.Common.StatusFeed
{
    public abstract class AStatusMessage : IStatusMessage
    {
        public DateTime Timestamp { get; } = DateTime.Now;
        public abstract string ShortDescription { get; }
        public abstract string ExtendedDescription { get; }
    }
}
