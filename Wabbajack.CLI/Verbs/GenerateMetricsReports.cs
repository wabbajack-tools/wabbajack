using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.NamingConventionBinder;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.DTOs.ServerResponses;
using Wabbajack.Paths;

namespace Wabbajack.CLI.Verbs;

public class GenerateMetricsReports : IVerb
{
    private readonly HttpClient _client;
    private readonly DTOSerializer _dtos;

    public GenerateMetricsReports(HttpClient client, DTOSerializer dtos)
    {
        _client = client;
        _dtos = dtos;
    }
    public Command MakeCommand()
    {
        var command = new Command("generate-metrics-report");
        command.Add(new Option<AbsolutePath>(new[] {"-o", "-output"}, "Output folder"));
        command.Description = "Generates usage metrics and outputs a html report about them";
        command.Handler = CommandHandler.Create(Run);
        return command;


    }
    
    private async Task<int> Run(AbsolutePath output)
    {
        var subjects = await GetMetrics("one day ago", "now", "finish_install")
            .Where(d => d.Action != d.Subject)
            .Select(async d => d.GroupingSubject)
            .ToHashSet();

        var allTime = await GetMetrics("10 days ago", "now", "finish_install")
            .Where(d => subjects.Contains(d.GroupingSubject))
            .ToList();

        var grouped = allTime.GroupBy(g => (g.Timestamp.ToString("yyyy-MM-dd"), g.GroupingSubject)).ToArray();
        
        

        return 0;
    }

    private async IAsyncEnumerable<MetricResult> GetMetrics(string start, string end, string action)
    {
        await using var response = await _client.GetStreamAsync(new Uri($"https://build.wabbajack.org/metrics/report?action={action}&from={start}&end={end}"));

        var sr = new StreamReader(response, leaveOpen: false);

        while (true)
        {
            var line = await sr.ReadLineAsync();
            if (line == null) break;
            yield return _dtos.Deserialize<MetricResult>(line)!;
        }
    }
}