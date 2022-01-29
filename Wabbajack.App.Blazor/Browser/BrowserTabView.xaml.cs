using System;
using System.Reactive.Disposables;
using System.Windows;
using System.Windows.Controls;
using ReactiveUI;

namespace Wabbajack.App.Blazor.Browser;

public partial class BrowserTabView : IDisposable
{
    private readonly CompositeDisposable _compositeDisposable;

    public BrowserTabView(BrowserTabViewModel vm)
    {
        _compositeDisposable = new CompositeDisposable();
        InitializeComponent();
        Browser.Browser.Source = new Uri("http://www.google.com");
        DataContext = vm;

        vm.WhenAnyValue(vm => vm.HeaderText)
            .BindTo(this, view => view.HeaderText.Text)
            .DisposeWith(_compositeDisposable);
    }

    public void Dispose()
    {
        _compositeDisposable.Dispose();
    }

    private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
    {
        var tc = (TabControl) this.Parent;
        tc.Items.Remove(this);
        this.Dispose();
    }
}

