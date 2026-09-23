using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Input;
using Avalonia.ReactiveUI;
using FluentIcons.Common;
using ReactiveUI;
using Wabbajack.App.Avalonia.Controls;
using Wabbajack.App.Avalonia.LoginManagers;
using Wabbajack.App.Avalonia.ViewModels.Settings;

namespace Wabbajack.App.Avalonia.Views.Settings;

/// <summary>
/// One login tile. The button reads "Logged in" in colour while logged in and turns into "Log out" under the
/// pointer, since that is what pressing it does; logged out it reads "Log in".
/// </summary>
public partial class LoginItemView : ReactiveUserControl<LoginTargetVM>
{
    public LoginItemView()
    {
        InitializeComponent();
        this.WhenActivated(disposables =>
        {
            this.WhenAnyValue(v => v.ViewModel!.Login.LoggedIn)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(_ => Apply(LoginButton.IsPointerOver))
                .DisposeWith(disposables);

            LoginButton.PointerEntered += OnPointerEntered;
            LoginButton.PointerExited += OnPointerExited;
            Disposable.Create(() =>
            {
                LoginButton.PointerEntered -= OnPointerEntered;
                LoginButton.PointerExited -= OnPointerExited;
            }).DisposeWith(disposables);
        });
    }

    private INeedsLogin? Login => ViewModel?.Login;

    private void OnPointerEntered(object? sender, PointerEventArgs e) => Apply(true);

    private void OnPointerExited(object? sender, PointerEventArgs e) => Apply(false);

    private void Apply(bool hovered)
    {
        if (Login is not { } login) return;

        if (!login.LoggedIn)
        {
            LoginButton.Text = "Log in";
            LoginButton.Icon = Symbol.PersonArrowRight;
            LoginButton.IconVariant = IconVariant.Regular;
            LoginButton.ButtonStyle = ButtonStyle.Mono;
        }
        else if (hovered)
        {
            LoginButton.Text = "Log out";
            LoginButton.Icon = Symbol.PersonArrowLeft;
            LoginButton.IconVariant = IconVariant.Regular;
            LoginButton.ButtonStyle = ButtonStyle.Color;
        }
        else
        {
            LoginButton.Text = "Logged in";
            LoginButton.Icon = Symbol.PersonAvailable;
            LoginButton.IconVariant = IconVariant.Filled;
            LoginButton.ButtonStyle = ButtonStyle.Color;
        }
    }
}
