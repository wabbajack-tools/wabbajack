using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Spectre.Console;

namespace Wabbajack.CLI.Console;

/// <summary>
/// Spectre.Console implementation of IConsoleRenderer.
/// </summary>
public class SpectreConsoleRenderer : IConsoleRenderer
{
    private readonly IAnsiConsole _console;

    public SpectreConsoleRenderer()
    {
        _console = AnsiConsole.Console;
    }

    public SpectreConsoleRenderer(IAnsiConsole console)
    {
        _console = console;
    }

    public async Task WithProgress(Func<IProgressContext, Task> action)
    {
        await _console.Progress()
            .AutoClear(false)
            .HideCompleted(false)
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new RemainingTimeColumn(),
                new SpinnerColumn())
            .StartAsync(async ctx =>
            {
                var wrapper = new SpectreProgressContext(ctx);
                await action(wrapper);
            });
    }

    public void Table<T>(string title, IEnumerable<T> items, params (string Header, Func<T, string> Selector)[] columns)
    {
        var table = new Table();
        table.Title = new TableTitle(title);
        table.Border = TableBorder.Rounded;

        foreach (var (header, _) in columns)
        {
            table.AddColumn(new TableColumn(header).Centered());
        }

        foreach (var item in items)
        {
            var row = new string[columns.Length];
            for (int i = 0; i < columns.Length; i++)
            {
                row[i] = columns[i].Selector(item);
            }
            table.AddRow(row);
        }

        _console.Write(table);
    }

    public void Success(string message)
    {
        _console.MarkupLine($"[green]✓[/] {Spectre.Console.Markup.Escape(message)}");
    }

    public void Error(string message)
    {
        _console.MarkupLine($"[red]✗[/] {Spectre.Console.Markup.Escape(message)}");
    }

    public void Info(string message)
    {
        _console.MarkupLine($"[blue]ℹ[/] {Spectre.Console.Markup.Escape(message)}");
    }

    public void Warning(string message)
    {
        _console.MarkupLine($"[yellow]⚠[/] {Spectre.Console.Markup.Escape(message)}");
    }

    public void WriteMarkup(string markup)
    {
        _console.Markup(markup);
    }

    public void WriteMarkupLine(string markup)
    {
        _console.MarkupLine(markup);
    }
}

internal class SpectreProgressContext : IProgressContext
{
    private readonly ProgressContext _ctx;

    public SpectreProgressContext(ProgressContext ctx)
    {
        _ctx = ctx;
    }

    public IProgressTask AddTask(string description, double maxValue = 100)
    {
        var task = _ctx.AddTask(description, maxValue: maxValue);
        return new SpectreProgressTask(task);
    }
}

internal class SpectreProgressTask : IProgressTask
{
    private readonly ProgressTask _task;

    public SpectreProgressTask(ProgressTask task)
    {
        _task = task;
    }

    public double Value
    {
        get => _task.Value;
        set => _task.Value = value;
    }

    public double MaxValue
    {
        get => _task.MaxValue;
        set => _task.MaxValue = value;
    }

    public string Description
    {
        get => _task.Description;
        set => _task.Description = value;
    }

    public void SetIndeterminate()
    {
        _task.IsIndeterminate = true;
    }

    public void Increment(double value)
    {
        _task.Increment(value);
    }

    public void Complete()
    {
        _task.Value = _task.MaxValue;
    }
}
