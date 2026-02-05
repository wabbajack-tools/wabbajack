using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Wabbajack.CLI.Console;

/// <summary>
/// Interface for rendering CLI output with progress bars, tables, and styled messages.
/// </summary>
public interface IConsoleRenderer
{
    /// <summary>
    /// Creates a progress context for displaying progress bars.
    /// </summary>
    Task WithProgress(Func<IProgressContext, Task> action);

    /// <summary>
    /// Renders a table with the given items and column definitions.
    /// </summary>
    void Table<T>(string title, IEnumerable<T> items, params (string Header, Func<T, string> Selector)[] columns);

    /// <summary>
    /// Renders a success message.
    /// </summary>
    void Success(string message);

    /// <summary>
    /// Renders an error message.
    /// </summary>
    void Error(string message);

    /// <summary>
    /// Renders an informational message.
    /// </summary>
    void Info(string message);

    /// <summary>
    /// Renders a warning message.
    /// </summary>
    void Warning(string message);

    /// <summary>
    /// Writes markup text to the console.
    /// </summary>
    void WriteMarkup(string markup);

    /// <summary>
    /// Writes a line of markup text to the console.
    /// </summary>
    void WriteMarkupLine(string markup);
}

/// <summary>
/// Context for managing progress tasks within a progress display.
/// </summary>
public interface IProgressContext
{
    /// <summary>
    /// Adds a new progress task.
    /// </summary>
    IProgressTask AddTask(string description, double maxValue = 100);
}

/// <summary>
/// Represents a single progress task within a progress display.
/// </summary>
public interface IProgressTask
{
    /// <summary>
    /// Gets or sets the current progress value.
    /// </summary>
    double Value { get; set; }

    /// <summary>
    /// Gets or sets the maximum progress value.
    /// </summary>
    double MaxValue { get; set; }

    /// <summary>
    /// Gets or sets the task description.
    /// </summary>
    string Description { get; set; }

    /// <summary>
    /// Sets the progress to indeterminate state (spinner).
    /// </summary>
    void SetIndeterminate();

    /// <summary>
    /// Increments the progress value.
    /// </summary>
    void Increment(double value);

    /// <summary>
    /// Marks the task as completed.
    /// </summary>
    void Complete();
}
