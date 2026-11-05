using System;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Wabbajack.App.Avalonia.Controls;
using Wabbajack.App.Avalonia.Interfaces;
using Wabbajack.Installer;

namespace Wabbajack.App.Avalonia.ViewModels;

/// <summary>
///     A yes-or-no question shown as a floating pane, which the installer asks through
///     <see cref="MainWindowVM.ShowConfirmationDialog" />. Closing the pane is a no. <see cref="Error" /> makes
///     the same pane an error the user can only dismiss.
/// </summary>
public partial class ConfirmationDialogVM : ViewModel, IClosableVM
{
    private readonly TaskCompletionSource<bool> _tcs = new();

    [Reactive] public partial string TypedConfirmation { get; set; }

    public ConfirmationDialogVM(string title, string message, bool severe = false,
        string? requiredConfirmationText = null)
    {
        TypedConfirmation = "";
        Title = title;
        Message = message;
        IsSevere = severe;
        RequiredConfirmationText = requiredConfirmationText;

        var canConfirm = string.IsNullOrEmpty(requiredConfirmationText)
            ? Observable.Return(true)
            : this.WhenAnyValue(vm => vm.TypedConfirmation)
                .Select(typed => string.Equals(typed?.Trim(), requiredConfirmationText,
                    StringComparison.OrdinalIgnoreCase));

        ConfirmCommand = ReactiveCommand.Create(() => { _tcs.TrySetResult(true); }, canConfirm);
        CancelCommand = ReactiveCommand.Create(() => { _tcs.TrySetResult(false); });
        CloseCommand = CancelCommand;
    }

    public ConfirmationDialogVM(ConfirmRequest request)
        : this(request.Title, request.Message, request.Severe, request.RequiredConfirmationText)
    {
    }

    public static ConfirmationDialogVM Error(string title, string message)
    {
        return new ConfirmationDialogVM(title, message)
        {
            IsError = true,
            CanCancel = false,
            ConfirmText = "OK",
            ConfirmStyle = ButtonStyle.Mono
        };
    }

    public string Title { get; }
    public string Message { get; }
    public bool IsError { get; private init; }
    public bool IsSevere { get; }
    public string? RequiredConfirmationText { get; }
    public bool NeedsTypedConfirmation => !string.IsNullOrEmpty(RequiredConfirmationText);
    public string TypedConfirmationPrompt => $"Type {RequiredConfirmationText} to continue";
    public bool CanCancel { get; private init; } = true;
    public string ConfirmText { get; private init; } = "Continue";
    public ButtonStyle ConfirmStyle { get; private init; } = ButtonStyle.Danger;
    public Task<bool> Result => _tcs.Task;

    public ICommand ConfirmCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand CloseCommand { get; }
}
