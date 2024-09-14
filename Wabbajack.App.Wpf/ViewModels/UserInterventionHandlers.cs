using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using Wabbajack.Common;
using Wabbajack.DTOs.Interventions;
using Wabbajack.Interventions;
using Wabbajack.Messages;

namespace Wabbajack;

public class UserInterventionHandlers
{
    public MainWindowVM MainWindow { get; }
    private AsyncLock _browserLock = new();
    private readonly ILogger<UserInterventionHandlers> _logger;

    public UserInterventionHandlers(ILogger<UserInterventionHandlers> logger, MainWindowVM mvm)
    {
        _logger = logger;
        MainWindow = mvm;
    }

    private async Task WrapBrowserJob(IUserIntervention intervention, WebBrowserVM vm, Func<WebBrowserVM, CancellationTokenSource, Task> toDo)
    {
        var wait = await _browserLock.WaitAsync();
        var cancel = new CancellationTokenSource();
        var oldPane = MainWindow.ActivePane;
        
        // TODO: FIX using var vm = await WebBrowserVM.GetNew(_logger);
        NavigateTo.Send(vm);
        vm.BackCommand = ReactiveCommand.Create(() =>
        {
            cancel.Cancel();
            NavigateTo.Send(oldPane);
            intervention.Cancel();
        });

        try
        {
            await toDo(vm, cancel);
        }
        catch (TaskCanceledException)
        {
            intervention.Cancel();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "During Web browser job");
            intervention.Cancel();
        }
        finally
        {
            wait.Dispose();
        }

        NavigateTo.Send(oldPane);
    }

    public async Task Handle(IStatusMessage msg)
    {
        switch (msg)
        {
            /*
            case RequestNexusAuthorization c:
                await WrapBrowserJob(c, async (vm, cancel) =>
                {
                    await vm.Driver.WaitForInitialized();
                    var key = await NexusApiClient.SetupNexusLogin(new CefSharpWrapper(vm.Browser), m => vm.Instructions = m, cancel.Token);
                    c.Resume(key);
                });
                break;
            case ManuallyDownloadNexusFile c:
                await WrapBrowserJob(c, (vm, cancel) => HandleManualNexusDownload(vm, cancel, c));
                break;
            case ManuallyDownloadFile c:
                await WrapBrowserJob(c, (vm, cancel) => HandleManualDownload(vm, cancel, c));
                break;
            case AbstractNeedsLoginDownloader.RequestSiteLogin c:
                await WrapBrowserJob(c, async (vm, cancel) =>
                {
                    await vm.Driver.WaitForInitialized();
                    var data = await c.Downloader.GetAndCacheCookies(new CefSharpWrapper(vm.Browser), m => vm.Instructions = m, cancel.Token);
                    c.Resume(data);
                });
                break;
            case RequestOAuthLogin oa:
                await WrapBrowserJob(oa, async (vm, cancel) =>
                {
                    await OAuthLogin(oa, vm, cancel);
                });


                break;
                */
            case CriticalFailureIntervention c:
                MessageBox.Show(c.ExtendedDescription, c.ShortDescription, MessageBoxButton.OK,
                    MessageBoxImage.Error);
                c.Cancel();
                if (c.ExitApplication) await MainWindow.ShutdownApplication();
                break;
            case ConfirmationIntervention c:
                break;
            default:
                throw new NotImplementedException($"No handler for {msg}");
        }
    }
    
}
