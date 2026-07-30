using Avalonia.Controls;

namespace Wabbajack;

// ColumnDefinition is not a Control, so Avalonia's name generator emits no field for it;
// resolve the navigation column from the owning grid instead.
public partial class MainWindow
{
    private ColumnDefinition NavigationColumn => ContentRootGrid.ColumnDefinitions[0];
}
