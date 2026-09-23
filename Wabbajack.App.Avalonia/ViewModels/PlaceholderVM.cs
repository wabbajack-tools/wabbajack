namespace Wabbajack.App.Avalonia.ViewModels;

/// <summary>
/// Stands in for a screen that has not been ported yet, so the nav rail can be driven end to end.
/// </summary>
public class PlaceholderVM(string title) : ViewModel
{
    public string Title { get; } = title;
}
