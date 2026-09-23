using System.Windows.Input;

namespace Wabbajack.App.Avalonia.Interfaces;

/// <summary>A floating pane that knows how to close itself; Escape and a click outside it both ask it to.</summary>
public interface IClosableVM
{
    ICommand CloseCommand { get; }
}

/// <summary>A pane that gets the title bar's Get Help button, and says what it does.</summary>
public interface ICanGetHelpVM
{
    ICommand GetHelpCommand { get; }
}

/// <summary>A pane that gets the title bar's Install from disk button, and says what it does.</summary>
public interface ICanLoadLocalFileVM
{
    ICommand LoadLocalFileCommand { get; }
}
