using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Wabbajack.App.Avalonia.ViewModels.Installers.Preflight;

namespace Wabbajack.App.Avalonia.Views.Installers.Preflight;

/// <summary>
///     Picks between a band header and an archive row for the one flat list that holds both, as the WPF
///     DataTemplateSelector of the same name did.
/// </summary>
public class ArchiveListTemplateSelector : IDataTemplate
{
    public IDataTemplate? RowTemplate { get; set; }
    public IDataTemplate? GroupTemplate { get; set; }

    public Control? Build(object? param)
    {
        return param switch
        {
            ArchiveGroupVM => GroupTemplate?.Build(param),
            ArchiveRowVM => RowTemplate?.Build(param),
            _ => null
        };
    }

    public bool Match(object? data) => data is ArchiveGroupVM or ArchiveRowVM;
}
