using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace Wabbajack;

// Maps a view-model instance to its view by naming convention (FooVM/FooViewModel -> FooView),
// replacing the DataTemplate DataType maps the WPF app used for screen switching. Views are
// resolved by simple name across the assembly; missing views fall back to a diagnostic TextBlock
// until they are integrated from port-staging.
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

        var type = Type.GetType($"Wabbajack.Views.{viewName}, Wabbajack")
                   ?? Type.GetType($"Wabbajack.{viewName}, Wabbajack");
        return type != null
            ? (Control)Activator.CreateInstance(type)!
            : new TextBlock { Text = "View not found: " + viewName };
    }

    public bool Match(object? data) => data is ViewModel;
}
