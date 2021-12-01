using System;
using System.Collections;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Toolkit.HighPerformance;
using Wabbajack.BuildServer;
using Wabbajack.Common;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.DTOs.ServerResponses;
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

    private IEnumerable<DateTime> GetDates(DateTime fromDate, DateTime toDate)
    {
        for (var d = new DateTime(fromDate.Year, fromDate.Month, fromDate.Day); d <= toDate; d = d.AddDays(1))
        {
            yield return d;
        }
    }


    public async IAsyncEnumerable<MetricResult> GetRecords(DateTime fromDate, DateTime toDate, string action)
    {
        var keys = new Dictionary<string, int>();
        int GetMetricKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return -1;
            if (keys.TryGetValue(key, out var v))
                return v;
            keys.Add(key, keys.Count);
            return keys.Count - 1;

        }
        
        foreach (var file in GetFiles(fromDate, toDate))
        {
            await foreach (var line in file.ReadAllLinesAsync())
            {
                var metric = _dtos.Deserialize<Metric>(line)!;
                if (metric.Action != action) continue;
                if (metric.Timestamp >= fromDate && metric.Timestamp <= toDate)
                {
                    yield return new MetricResult
                    {
                        Timestamp = metric.Timestamp,
                        Subject = metric.Subject,
                        Action = metric.Action,
                        MetricKey = GetMetricKey(metric.MetricsKey),
                        UserAgent = metric.UserAgent,
                        GroupingSubject = GetGroupingSubject(metric.Subject)
                    };
                }
            }
        }
    }

    private Regex groupingRegex = new("^[^0-9]*");
    private string GetGroupingSubject(string metricSubject)
    {
        try
        {
            var result = groupingRegex.Match(metricSubject).Groups[0].ToString();
            return string.IsNullOrEmpty(result) ? metricSubject : result;
        }
        catch (Exception)
        {
            return metricSubject;
        }
    }

    private IEnumerable<AbsolutePath> GetFiles(DateTime fromDate, DateTime toDate)
    {
        var folder = _settings.MetricsFolder.ToAbsolutePath();
        foreach (var day in GetDates(fromDate, toDate))
        {
            var file = folder.Combine(day.ToString("yyyy_MM_dd") + ".json");
            if (file.FileExists())
                yield return file;
        }
    }

}