using System;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MahApps.Metro.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.Wpf;
using ReactiveUI;
using Wabbajack.Common;

namespace Wabbajack;

public partial class BrowserWindow : MetroWindow
{
    private readonly CompositeDisposable _disposable;
    private readonly IServiceProvider _serviceProvider;
    public WebView2 Browser { get; set; }

    public BrowserWindow(IServiceProvider serviceProvider)
    {
        InitializeComponent();

        _disposable = new CompositeDisposable();
        _serviceProvider = serviceProvider;
        Browser = _serviceProvider.GetRequiredService<WebView2>();
        RxApp.MainThreadScheduler.Schedule(() =>
        {
            if(Browser.Parent != null)
            {
                ((Panel)Browser.Parent).Children.Remove(Browser);
            }
            MainGrid.Children.Add(Browser);
            Grid.SetRow(Browser, 3);
            Grid.SetColumnSpan(Browser, 3);
        });
    }

    private void UIElement_OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            base.DragMove();
        }
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
            .ContinueWith(_ => Dispatcher.Invoke(() =>
            {
                Close();
            }));
    }
}