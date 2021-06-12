using System;

namespace Wabbajack.Common.StatusFeed
{
    public interface IStatusMessage
    {
        DateTime Timestamp { get; }
        string ShortDescription { get; }
        string ExtendedDescription { get; }
    }
}
