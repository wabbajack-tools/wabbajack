using System;
using CouchDB.Driver.Types;
using Microsoft.Extensions.Primitives;

namespace Wabbajack.Server.DTOs;

public class Metric : CouchDocument
{
    public DateTime Timestamp { get; set; }
    public string Action { get; set; }
    public string Subject { get; set; }
    public string MetricsKey { get; set; }
    public string UserAgent { get; set; }
    public string Ip { get; set; }
}