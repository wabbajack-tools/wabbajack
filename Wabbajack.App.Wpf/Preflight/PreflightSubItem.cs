// Wabbajack.App.Wpf/Preflight/PreflightSubItem.cs
using System.Windows.Input;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace Wabbajack.Preflight;

public partial class PreflightSubItem : ReactiveObject
{
    public required string Name { get; init; }
    public required string SizeText { get; init; }
    [Reactive] public partial string? StatusText { get; set; }
    [Reactive] public partial double? Progress { get; set; }
    public ICommand? ActionCommand { get; init; }
    public string? ActionLabel { get; init; }
}
