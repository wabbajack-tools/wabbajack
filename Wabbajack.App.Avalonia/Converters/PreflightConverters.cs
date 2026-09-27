using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using FluentIcons.Common;
using Wabbajack.Installer.Preflight;

namespace Wabbajack.App.Avalonia.Converters;

/// <summary>
///     What the WPF archive row templates did with DataTriggers on State and IsExpanded: the icon and its
///     colour for each state, whether the progress fill shows, and which way a band header's chevron points.
/// </summary>
public static class PreflightConverters
{
    public static readonly FuncValueConverter<ArchiveState, Symbol> StateSymbol = new(state => state switch
    {
        ArchiveState.Present or ArchiveState.Downloaded => Symbol.CheckmarkCircle,
        ArchiveState.Downloading => Symbol.ArrowDownload,
        ArchiveState.ManualInProgress => Symbol.ArrowSync,
        ArchiveState.ManualRequired => Symbol.Open,
        ArchiveState.Failed or ArchiveState.Unsupported => Symbol.ErrorCircle,
        _ => Symbol.Circle
    });

    public static readonly FuncValueConverter<ArchiveState, IBrush?> StateBrush = new(state => Brush(state switch
    {
        ArchiveState.Present or ArchiveState.Downloaded => "SuccessBrush",
        ArchiveState.Downloading or ArchiveState.ManualInProgress => "PrimaryBrush",
        ArchiveState.ManualRequired => "WarningBrush",
        ArchiveState.Failed or ArchiveState.Unsupported => "ErrorBrush",
        _ => "ComplementaryWhite25Brush"
    }));

    public static readonly FuncValueConverter<ArchiveState, bool> StateShowsProgress =
        new(state => state is ArchiveState.Downloading or ArchiveState.ManualInProgress);

    public static readonly FuncValueConverter<bool, Symbol> Chevron =
        new(expanded => expanded ? Symbol.ChevronDown : Symbol.ChevronRight);

    /// <summary>An empty tooltip is no tooltip, as WPF's trigger switching ToolTipService off for "" had it.</summary>
    public static readonly FuncValueConverter<string?, string?> EmptyToNull =
        new(text => string.IsNullOrEmpty(text) ? null : text);

    public static IBrush? Brush(string key) =>
        Application.Current is { } app && app.TryGetResource(key, app.ActualThemeVariant, out var value)
            ? value as IBrush
            : null;
}
