using System;
using System.Reactive.Disposables;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ReactiveUI;
using Wabbajack.Common;

namespace Wabbajack.Views;

public partial class BrowserTabView : IDisposable
{
    private readonly CompositeDisposable _compositeDisposable;

    public BrowserTabView(BrowserTabViewModel vm)
    {
        _compositeDisposable = new CompositeDisposable();
        InitializeComponent();
        Browser.Browser.Source = new Uri("http://www.google.com");
        vm.Browser = Browser;
        DataContext = vm;

        vm.WhenAnyValue(vm => vm.HeaderText)
            .BindTo(this, view => view.HeaderText.Text)
            .DisposeWith(_compositeDisposable);

        Start().FireAndForget();
    }

    private async Task Start()
    {
        await ((BrowserTabViewModel) DataContext).RunWrapper(CancellationToken.None);
        ClickClose(this, new RoutedEventArgs());
    }

    public void Dispose()
    {
        _compositeDisposable.Dispose();
        var vm = (BrowserTabViewModel) DataContext;
        vm.Browser = null;
    }

    private void ClickClose(object sender, RoutedEventArgs e)
    {
        var tc = (TabControl) this.Parent;
        if (tc != null)
        {
            if (tc.Items.Contains(this))
                tc.Items.Remove(this);
        }

        this.Dispose();
    }
}

