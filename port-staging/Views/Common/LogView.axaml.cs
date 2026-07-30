using System;
using Avalonia.Controls;
using DynamicData;
using DynamicData.Binding;
using Wabbajack.Models;
using static Wabbajack.Models.LogStream;

namespace Wabbajack;

/// <summary>
/// Interaction logic for LogView.axaml
/// </summary>
public partial class LogView : UserControl
{
    // Holds the DynamicData subscription that keeps LogListBox.ItemsSource filtered/live;
    // torn down and rebuilt whenever DataContext changes, and disposed when the control
    // leaves the visual tree.
    private IDisposable? _filterSubscription;

    public LogView()
    {
        InitializeComponent();

        DataContextChanged += LogView_DataContextChanged;
        DetachedFromVisualTree += (_, _) =>
        {
            _filterSubscription?.Dispose();
            _filterSubscription = null;
        };
    }

    // The original WPF view declared a <CollectionViewSource Source="{Binding
    // LoggerProvider.MessageLog}" Filter="CollectionViewSource_Filter"/> in
    // UserControl.Resources, which resolved "LoggerProvider" reflectively off whatever
    // DataContext was in scope (CompilerMainVM, CompilerDetailsVM, InstallationVM, ...
    // -- there is no shared interface between them, each just happens to declare its own
    // `LogStream LoggerProvider` property). Avalonia has no CollectionViewSource, so this
    // reproduces both the reflective property lookup and the filtering by hand:
    // DynamicData's .Filter() operator replaces the CollectionViewSource_Filter callback,
    // and the resulting live filtered collection is assigned straight to the ListBox.
    private void LogView_DataContextChanged(object? sender, EventArgs e)
    {
        _filterSubscription?.Dispose();
        _filterSubscription = null;
        LogListBox.ItemsSource = null;

        var loggerProviderProperty = DataContext?.GetType().GetProperty("LoggerProvider");
        if (loggerProviderProperty?.GetValue(DataContext) is not LogStream loggerProvider)
            return;

        _filterSubscription = loggerProvider.MessageLog
            .ToObservableChangeSet()
            .Filter(CollectionViewSource_Filter)
            .Bind(out var filteredRows)
            .Subscribe();

        LogListBox.ItemsSource = filteredRows;
    }

    // Kept as a named method (rather than an inline lambda) for parity with the WPF
    // code-behind's CollectionViewSource_Filter handler, which this replaces.
    private static bool CollectionViewSource_Filter(ILogMessage row) => row.Level.Ordinal >= 2;
}
