using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.ReactiveUI;
using ReactiveUI;

namespace Wabbajack.Views;

public partial class BrowserWindow : ReactiveUserControl<BrowserWindowViewModel>
{
    public BrowserWindow()
    {
        InitializeComponent();

        this.WhenActivated(disposables =>
        {
            // Re-parent the shared WebView2 into this view whenever the view model swaps it in,
            // mirroring the WPF BrowserWindow behaviour.
            this.WhenAnyValue(x => x.ViewModel!.Browser)
                .Where(browser => browser is not null)
                .Subscribe(browser =>
                {
                    if (browser!.Parent is Panel oldParent)
                        oldParent.Children.Remove(browser);

                    WebViewGrid.Children.Clear();
                    WebViewGrid.Children.Add(browser);
                })
                .DisposeWith(disposables);

            Disposable.Create(() => WebViewGrid.Children.Clear()).DisposeWith(disposables);
        });
    }
}
