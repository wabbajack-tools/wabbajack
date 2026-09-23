namespace Wabbajack.App.Avalonia.ViewModels;

/// <summary>
/// Stands in for a screen that has not been ported yet, so the nav rail can be driven end to end.
/// </summary>
public class PlaceholderViewModel(string title) : ViewModelBase
{
    public string Title { get; } = title;
}
