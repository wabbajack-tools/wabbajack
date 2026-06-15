using System.Windows;

namespace Wabbajack;

/// <summary>WPF (MessageBox) implementation of <see cref="IDialogService"/>.</summary>
public sealed class WpfDialogService : IDialogService
{
    public void ShowError(string message, string title) =>
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
}
