using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Wabbajack.ViewModels;

namespace Wabbajack;

// Maps a *ViewModel instance to its *View by naming convention, matching the
// DataTemplate DataType maps the WPF app used for screen switching.
public class ViewLocator : IDataTemplate
{
    public Control Build(object? data)
    {
        var name = data!.GetType().FullName!.Replace("ViewModel", "View", StringComparison.Ordinal);
        var type = Type.GetType(name);
        return type != null
            ? (Control)Activator.CreateInstance(type)!
            : new TextBlock { Text = "View not found: " + name };
    }

    public bool Match(object? data) => data is ViewModelBase;
}
