using System.Windows;

namespace Wabbajack;

/// <summary>WPF (MessageBox) implementation of <see cref="IDialogService"/>.</summary>
public sealed class WpfDialogService : IDialogService
{
    public void ShowError(string message, string title) =>
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);

    public System.Threading.Tasks.Task<bool> ShowConfirmation(string title, string message) =>
        System.Threading.Tasks.Task.FromResult(
            MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes);
}
