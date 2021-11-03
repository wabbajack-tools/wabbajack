using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using ReactiveUI.Validation.Helpers;
using Wabbajack.App.Interfaces;
using Wabbajack.App.Messages;
using Wabbajack.App.Models;
using Wabbajack.App.Screens;
using Wabbajack.App.Views;
using Wabbajack.Common;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;

namespace Wabbajack.App.ViewModels;

public class MainWindowViewModel : ReactiveValidationObject, IActivatableViewModel, IDisposable
{
    private readonly InstallationStateManager _manager;
    private readonly IServiceProvider _provider;
    private readonly Task _resourcePoller;
    private readonly IResource[] _resources;
    private readonly IEnumerable<IScreenView> _screens;
    private StatusReport[] _prevReport;

    private CompositeDisposable VMDisposables = new();

    public MainWindowViewModel(IEnumerable<IScreenView> screens, IEnumerable<IResource> resources,
        IServiceProvider provider,
        InstallationStateManager manager)
    {
        _provider = provider;
        _screens = screens;
        _resources = resources.ToArray();
        _manager = manager;

        _prevReport = NextReport();

        Activator = new ViewModelActivator();

        MessageBus.Current.Listen<NavigateTo>()
            .Subscribe(Receive)
            .DisposeWith(VMDisposables);

        MessageBus.Current.Listen<NavigateBack>()
            .Subscribe(Receive)
            .DisposeWith(VMDisposables);

        _resourcePoller = StartResourcePoller(TimeSpan.FromSeconds(0.25));

        this.WhenActivated(disposables =>
        {
            BackButton = ReactiveCommand.Create(() => { Receive(new NavigateBack()); },
                    this.WhenAnyValue(vm => vm.BreadCrumbs)
                        .Select(bc => bc.Count() > 1))
                .DisposeWith(disposables);

            SettingsButton = ReactiveCommand.Create(() => { Receive(new NavigateTo(typeof(SettingsViewModel))); })
                .DisposeWith(disposables);

            LogViewButton = ReactiveCommand.Create(() => { Receive(new NavigateTo(typeof(LogScreenViewModel))); })
                .DisposeWith(disposables);
        });
        CurrentScreen = (Control) _screens.First(s => s.ViewModelType == typeof(ModeSelectionViewModel));

        LoadFirstScreen().FireAndForget();
    }

    [Reactive] public Control CurrentScreen { get; set; }

    [Reactive] private ImmutableStack<Control> BreadCrumbs { get; set; } = ImmutableStack<Control>.Empty;

    [Reactive] public ReactiveCommand<Unit, Unit> BackButton { get; set; }

    [Reactive] public ReactiveCommand<Unit, Unit> SettingsButton { get; set; }

    [Reactive] public ReactiveCommand<Unit, Unit> LogViewButton { get; set; }

    [Reactive] public string ResourceStatus { get; set; }

    public ViewModelActivator Activator { get; }

    public void Receive(NavigateBack val)
    {
        CurrentScreen = BreadCrumbs.Peek();
        BreadCrumbs = BreadCrumbs.Pop();
    }

    public void Receive(NavigateTo val)
    {
        BreadCrumbs = BreadCrumbs.Push(CurrentScreen);

        if (val.ViewModel.IsAssignableTo(typeof(GuidedWebViewModel)))
            CurrentScreen = new GuidedWebView {ViewModel = (GuidedWebViewModel) _provider.GetService(val.ViewModel)!};
        else
            CurrentScreen = (Control) _screens.First(s => s.ViewModelType == val.ViewModel);
    }

    private async Task LoadFirstScreen()
    {
        var setting = await _manager.GetLastState();
        if (setting.Install != default && setting.Install.DirectoryExists())
        {
            BreadCrumbs =
                BreadCrumbs.Push((Control) _screens.First(s => s.ViewModelType == typeof(ModeSelectionViewModel)));

            MessageBus.Current.SendMessage(new ConfigureLauncher(setting.Install));
            Receive(new NavigateTo(typeof(LauncherViewModel)));
        }
        else
        {
            Receive(new NavigateTo(typeof(ModeSelectionViewModel)));
        }
    }

    private StatusReport[] NextReport()
    {
        return _resources.Select(r => r.StatusReport).ToArray();
    }

    private async Task StartResourcePoller(TimeSpan span)
    {
        while (true)
        {
            var report = NextReport();
            var sb = new StringBuilder();
            foreach (var (prev, next, limiter) in _prevReport.Zip(report, _resources))
            {
                var throughput = next.Transferred - prev.Transferred;
                if (throughput != 0)
                    sb.Append(
                        $"{limiter.Name}: [{next.Running}/{next.Pending + next.Running}] {throughput.ToFileSizeString()}/sec ");
            }

            ResourceStatus = sb.ToString();
            _prevReport = report;

            await Task.Delay(TimeSpan.FromSeconds(0.5));
        }
    }

    public void Dispose()
    {
        _resourcePoller.Dispose();
        VMDisposables.Dispose();
    }
}