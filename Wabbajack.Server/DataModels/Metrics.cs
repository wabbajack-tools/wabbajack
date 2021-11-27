using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Toolkit.HighPerformance;
using Wabbajack.BuildServer;
using Wabbajack.Common;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.Server.DTOs;

namespace Wabbajack.Server.DataModels;

public class Metrics
{
    private readonly AppSettings _settings;
    public SemaphoreSlim _lock = new(1);
    private readonly DTOSerializer _dtos;

    public Metrics(AppSettings settings, DTOSerializer dtos)
    {
        _settings = settings;
        _dtos = dtos;
    }

    public async Task Ingest(Metric metric)
    {
        using var _ = await _lock.Lock();
        var data = Encoding.UTF8.GetBytes(_dtos.Serialize(metric));
        var metricsFile = _settings.MetricsFolder.ToAbsolutePath().Combine(DateTime.Now.ToString("yyyy_MM_dd") + ".json");
        await using var fs = metricsFile.Open(FileMode.Append, FileAccess.Write, FileShare.Read);
        fs.Write(data);
        fs.Write(Encoding.UTF8.GetBytes("\n"));
    }
    
    
}