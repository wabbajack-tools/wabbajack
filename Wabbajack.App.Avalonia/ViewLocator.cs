using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using ReactiveUI;

namespace Wabbajack;

// Maps a view-model instance to its view by naming convention (FooVM / FooViewModel -> FooView),
// replacing the WPF DataTemplate DataType map that drove screen switching. Views are ReactiveUI
// IViewFor implementations, so the view model is assigned explicitly as well as via DataContext.
public class ViewLocator : IDataTemplate
{
    public Control Build(object? data)
    {
        if (data is null) return new TextBlock { Text = "null" };

        var vmName = data.GetType().Name;
        var viewName = vmName.EndsWith("ViewModel", StringComparison.Ordinal)
            ? vmName[..^"ViewModel".Length] + "View"
            : vmName.EndsWith("VM", StringComparison.Ordinal)
                ? vmName[..^"VM".Length] + "View"
                : vmName + "View";

        var type = Type.GetType($"Wabbajack.{viewName}, Wabbajack")
                   ?? Type.GetType($"Wabbajack.Views.{viewName}, Wabbajack");

        if (type is null)
            return new TextBlock { Text = "View not found: " + viewName };

        try
        {
            var control = (Control)Activator.CreateInstance(type)!;
            if (control is IViewFor viewFor)
                viewFor.ViewModel = data;
            control.DataContext = data;
            return control;
        }
        catch (Exception ex)
        {
            // Surface view construction failures instead of silently keeping the previous screen.
            System.Diagnostics.Trace.WriteLine($"ViewLocator failed to build {viewName}: {ex}");
            Console.Error.WriteLine($"ViewLocator failed to build {viewName}: {ex}");
            return new TextBlock { Text = $"Failed to build {viewName}: {ex.Message}" };
        }
    }

    public bool Match(object? data) => data is ViewModel;
}
