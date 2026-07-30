using System;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.ReactiveUI;
using ReactiveUI;

namespace Wabbajack;

public partial class BrowserWindow : ReactiveUserControl<BrowserWindowViewModel>
{
    public BrowserWindow()
    {
        InitializeComponent();

        this.WhenActivated(disposables =>
        {
            RxApp.MainThreadScheduler.Schedule(async () =>
            {
                WebViewWarning.IsVisible = false;
                await Task.Delay(TimeSpan.FromSeconds(2));
                WebViewWarning.IsVisible = true;
            });

            this.BindCommand(ViewModel, vm => vm.BackCommand, v => v.BackButton)
                .DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.CloseCommand, v => v.CloseButton)
                .DisposeWith(disposables);

            this.WhenAnyValue(v => v.ViewModel.HeaderText)
                .BindToStrict(this, view => view.Header.Text)
                .DisposeWith(disposables);

            this.WhenAnyValue(v => v.ViewModel.Instructions)
                .BindToStrict(this, view => view.Instructions.Text)
                .DisposeWith(disposables);

            this.WhenAnyValue(v => v.ViewModel.Address)
                .BindToStrict(this, view => view.AddressBar.Text)
                .DisposeWith(disposables);

            // TODO: ViewModel.Browser is still typed as Microsoft.Web.WebView2.Wpf.WebView2 in the
            // (not-yet-ported) BrowserWindowViewModel. Once that VM is ported to Avalonia with an
            // Avalonia-compatible WebView control, `browser` here needs to be an Avalonia.Controls.Control
            // (or the specific WebView control's type) for the Panel/Children logic below to compile.
            this.WhenAnyValue(x => x.ViewModel.Browser)
                .WhereNotNull()
                .ObserveOnGuiThread()
                .Subscribe(browser =>
                {
                    RxApp.MainThreadScheduler.Schedule(() =>
                    {
                        if (browser.Parent != null)
                        {
                            ((Panel)browser.Parent).Children.Remove(browser);
                        }
                        ViewModel.Browser.IsVisible = true;
                        ViewModel.Browser.Width = double.NaN;
                        ViewModel.Browser.Height = double.NaN;
                        WebViewGrid.Children.Add(browser);
                    });
                })
                .DisposeWith(disposables);
        });
    }
}
