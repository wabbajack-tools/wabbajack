using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Wabbajack.Common;
using Wabbajack.RateLimiter;

namespace Wabbajack.CLI.Console;

/// <summary>
/// Renders live progress information using Spectre.Console instead of text logging.
/// </summary>
public class SpectreRateLimiterReporter : IDisposable
{
    private readonly IEnumerable<IResource> _limiters;
    private readonly ILogger<SpectreRateLimiterReporter> _logger;
    private readonly CancellationTokenSource _cts;
    private StatusReport[] _prevReport;
    private readonly IAnsiConsole _console;
    private Table? _statusTable;
    private bool _isRunning;

    public SpectreRateLimiterReporter(ILogger<SpectreRateLimiterReporter> logger, IEnumerable<IResource> limiters)
    {
        _logger = logger;
        _limiters = limiters.ToArray();
        _console = AnsiConsole.Console;
        _cts = new CancellationTokenSource();
        _prevReport = NextReport();
    }

    public bool IsEnabled { get; private set; }

    /// <summary>
    /// Starts the live display. Call this at the beginning of long-running operations.
    /// </summary>
    public void Start()
    {
        if (_isRunning) return;
        _isRunning = true;
        IsEnabled = true;
        _prevReport = NextReport();

        // Start background render loop
        Task.Run(RenderLoop, _cts.Token);
    }

    /// <summary>
    /// Stops the live display.
    /// </summary>
    public void Stop()
    {
        _isRunning = false;
        IsEnabled = false;
    }

    private async Task RenderLoop()
    {
        try
        {
            await _console.Live(CreateStatusTable())
                .AutoClear(true)
                .Overflow(VerticalOverflow.Ellipsis)
                .StartAsync(async ctx =>
                {
                    while (_isRunning && !_cts.Token.IsCancellationRequested)
                    {
                        UpdateTable();
                        ctx.Refresh();
                        await Task.Delay(250, _cts.Token);
                    }
                });
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error in rate limiter display");
        }
    }

    private Table CreateStatusTable()
    {
        _statusTable = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey)
            .AddColumn(new TableColumn("[bold]Resource[/]").LeftAligned())
            .AddColumn(new TableColumn("[bold]Status[/]").Centered())
            .AddColumn(new TableColumn("[bold]Throughput[/]").RightAligned());

        foreach (var limiter in _limiters)
        {
            _statusTable.AddRow(limiter.Name, "-", "-");
        }

        return _statusTable;
    }

    private void UpdateTable()
    {
        if (_statusTable == null) return;

        var report = NextReport();
        _statusTable.Rows.Clear();

        foreach (var (prev, next, limiter) in _prevReport.Zip(report, _limiters))
        {
            var throughput = next.Transferred - prev.Transferred;
            var status = $"[blue]{next.Running}[/]/[grey]{next.Pending}[/]";

            string throughputText;
            if (throughput > 0)
            {
                throughputText = $"[green]{throughput.ToFileSizeString()}/s[/]";
            }
            else if (next.Running > 0)
            {
                throughputText = "[yellow]working...[/]";
            }
            else
            {
                throughputText = "[grey]idle[/]";
            }

            _statusTable.AddRow(
                Spectre.Console.Markup.Escape(limiter.Name),
                status,
                throughputText
            );
        }

        _prevReport = report;
    }

    private StatusReport[] NextReport()
    {
        return _limiters.Select(r => r.StatusReport).ToArray();
    }

    public void Dispose()
    {
        Stop();
        _cts.Cancel();
        _cts.Dispose();
    }
}
