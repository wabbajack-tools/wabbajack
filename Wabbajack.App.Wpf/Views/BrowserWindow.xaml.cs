using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using MahApps.Metro.Controls;
using ReactiveUI;
using Wabbajack.Common;

namespace Wabbajack;

public partial class BrowserWindow : MetroWindow
{
    private readonly CompositeDisposable _disposable;

    public BrowserWindow()
    {
        InitializeComponent();

        _disposable = new CompositeDisposable();
    }

    private void UIElement_OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        base.DragMove();
    }

    private void BrowserWindow_OnActivated(object sender, EventArgs e)
    {
        var vm = ((BrowserWindowViewModel) DataContext);
        vm.Browser = this;

        vm.WhenAnyValue(vm => vm.HeaderText)
            .BindToStrict(this, view => view.Header.Text)
            .DisposeWith(_disposable);
        
        vm.WhenAnyValue(vm => vm.Instructions)
            .BindToStrict(this, view => view.Instructions.Text)
            .DisposeWith(_disposable);
        
        vm.WhenAnyValue(vm => vm.Address)
            .BindToStrict(this, view => view.AddressBar.Text)
            .DisposeWith(_disposable);
        
        this.CopyButton.Command = ReactiveCommand.Create(() =>
        {
            Clipboard.SetText(vm.Address.ToString());
        });
        
        this.BackButton.Command = ReactiveCommand.Create(() =>
        {
            Browser.GoBack();
        });
        
        vm.RunWrapper(CancellationToken.None)
            .ContinueWith(_ => Dispatcher.Invoke(Close));
    }
}