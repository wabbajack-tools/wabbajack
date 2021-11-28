using System;
using Microsoft.Extensions.Primitives;

namespace Wabbajack.Server.DTOs;

public class Metric
{
    public DateTime Timestamp { get; set; }
    public string Action { get; set; }
    public string Subject { get; set; }
    public string MetricsKey { get; set; }
    public string UserAgent { get; set; }
}