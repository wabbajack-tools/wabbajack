using System;

namespace Wabbajack.DTOs.ServerResponses;


public class MetricResult
{
    public string Action { get; set; }
    public string Subject { get; set; }
    public string GroupingSubject { get; set; }
    public ulong MetricKey { get; set; }
    public string UserAgent { get; set; }
    public DateTime Timestamp { get; set; }
}