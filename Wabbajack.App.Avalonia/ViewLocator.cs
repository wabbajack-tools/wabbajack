using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Wabbajack.App.Avalonia.ViewModels;

namespace Wabbajack.App.Avalonia;

public class ViewLocator : IDataTemplate
{
    public bool SupportsRecycling => false;

    public Control? Build(object? data)
    {
        if (data is null) return null;
        var name = data.GetType().FullName!
            .Replace("ViewModels", "Views")
            .Replace("ViewModel", "View");
        // Also handle short "VM" suffix (e.g. ModListGalleryVM → ModListGalleryView)
        if (name.EndsWith("VM"))
            name = name[..^2] + "View";
        var type = Type.GetType(name);

        if (type != null)
            return (Control)Activator.CreateInstance(type)!;
        return new TextBlock { Text = "Not Found: " + name };
    }

    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}
