using System;

namespace Wabbajack.Interventions;

public interface IStatusMessage
{
    DateTime Timestamp { get; }
    string ShortDescription { get; }
    string ExtendedDescription { get; }
}
