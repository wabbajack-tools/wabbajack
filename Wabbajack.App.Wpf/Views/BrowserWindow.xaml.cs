using System;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Windows.Controls;
using System.Windows;
using ReactiveUI;
using System.Threading.Tasks;

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
                WebViewWarning.Visibility = Visibility.Collapsed;
                await Task.Delay(TimeSpan.FromSeconds(2));
                WebViewWarning.Visibility = Visibility.Visible;
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
                        ViewModel.Browser.Visibility = Visibility.Visible;
                        ViewModel.Browser.Width = double.NaN;
                        ViewModel.Browser.Height = double.NaN;
                        WebViewGrid.Children.Add(browser);
                    });
                })
                .DisposeWith(disposables);
        });
    }
}