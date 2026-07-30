using Avalonia.Controls;

namespace Wabbajack;

// ColumnDefinition is not a Control, so Avalonia's name generator does not emit fields for the
// named columns; resolve them from the owning grid's ColumnDefinitions instead.
public partial class InstallationView
{
    private ColumnDefinition InstallationLeftColumn => InstallationGrid.ColumnDefinitions[0];
    private ColumnDefinition InstallationRightColumn => InstallationGrid.ColumnDefinitions[2];
}
