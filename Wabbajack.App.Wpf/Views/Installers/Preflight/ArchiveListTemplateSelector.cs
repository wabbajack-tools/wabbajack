using System.Windows;
using System.Windows.Controls;

namespace Wabbajack;

/// <summary>
///     Picks between a band header and an archive row for the one flat list that holds both. The choice is
///     made by hand rather than left to an implicit DataTemplate: both view models are ReactiveObjects, and
///     an implicit template for those is already in scope further up the tree, which would win.
/// </summary>
public class ArchiveListTemplateSelector : DataTemplateSelector
{
    public DataTemplate? RowTemplate { get; set; }
    public DataTemplate? GroupTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object item, DependencyObject container)
    {
        return item switch
        {
            ArchiveGroupVM => GroupTemplate,
            ArchiveRowVM => RowTemplate,
            _ => base.SelectTemplate(item, container)
        };
    }
}
