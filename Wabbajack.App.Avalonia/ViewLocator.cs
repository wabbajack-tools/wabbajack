using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using ReactiveUI;

namespace Wabbajack;

public class ViewLocator : IDataTemplate
{
    public Control Build(object? data)
    {
        if (data is null) return new TextBlock { Text = "null" };
        var name = data.GetType().FullName!.Replace("ViewModel", "View").Replace("VM", "View");
        var type = Type.GetType(name) ?? Type.GetType(name + ", WabbajackAvalonia");
        if (type != null) return (Control) Activator.CreateInstance(type)!;
        return new TextBlock { Text = "Not Found: " + name };
    }

    public bool Match(object? data) => data is IReactiveObject;
}
