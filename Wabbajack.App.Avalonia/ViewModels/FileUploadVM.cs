using System;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Wabbajack.App.Avalonia.Interfaces;
using Wabbajack.App.Avalonia.Messages;
using Wabbajack.App.Avalonia.Services;
using Wabbajack.App.Avalonia.Util;
using Wabbajack.App.Avalonia.ViewModels.Common;
using Wabbajack.DTOs.Logins;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.RateLimiter;
using Wabbajack.Services.OSIntegrated.TokenProviders;

namespace Wabbajack.App.Avalonia.ViewModels;

/// <summary>
/// The Wabbajack CDN upload pane Settings opens for list authors: pick or drop a file, watch it upload, and copy
/// the URL it was saved to.
/// </summary>
public partial class FileUploadVM : ViewModel, IClosableVM
{
    private readonly ILogger<FileUploadVM> _logger;
    private readonly WabbajackApiTokenProvider _tokenProvider;
    private readonly Client _wjClient;
    private readonly Subject<bool> _isUploading = new();

    public FileUploadVM(ILogger<FileUploadVM> logger, WabbajackApiTokenProvider tokenProvider, Client wjClient)
    {
        _logger = logger;
        _tokenProvider = tokenProvider;
        _wjClient = wjClient;
        IsUploading = _isUploading;
        Picker = new FilePickerVM(this);

        Task.Run(async () =>
        {
            ApiToken = await _tokenProvider.Get();
            BrowseUploadsCommand = ReactiveCommand.Create(() =>
            {
                var authorApiKey = ApiToken?.AuthorKey;
                UIUtils.OpenWebsite(new Uri($"{Links.BuildServer}author_controls/login/{authorApiKey}"));
            });
        });

        BrowseFileCommand = Picker.ConstructTypicalPickerCommand(IsUploading.StartWith(false).Select(u => !u));
        // WPF's dialog was modal, so the upload started once a file had been chosen; this one is awaited instead.
        BrowseAndUploadFileCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            if (BrowseFileCommand is ReactiveCommand<Unit, Unit> browse)
                await browse.Execute();
            UploadCommand!.Execute(null);
        });

        CopyUrlCommand = ReactiveCommand.Create(() =>
        {
            var clipboard = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)
                ?.MainWindow?.Clipboard;
            _ = clipboard?.SetTextAsync(FileUrl);
        });

        UploadCommand = ReactiveCommand.Create(async () =>
            {
                _isUploading.OnNext(true);
                try
                {
                    var (progress, task) = await _wjClient.UploadAuthorFile(Picker.TargetPath);

                    var disposable = progress.Subscribe(m =>
                    {
                        FileUrl = m.Message;
                        if (m.PercentDone != Percent.Zero) UploadProgress = (double)m.PercentDone;
                    });

                    var final = await task;
                    disposable.Dispose();
                    FileUrl = final.ToString();
                }
                catch (Exception ex)
                {
                    _logger.LogError("Failed to upload file to CDN: {ex}", ex.ToString());
                    FileUrl = ex.ToString();
                }
                finally
                {
                    FileUrl = FileUrl.Replace(" ", "%20");
                    _isUploading.OnNext(false);
                }
            }, IsUploading.StartWith(false).Select(u => !u)
                .CombineLatest(Picker.WhenAnyValue(t => t.TargetPath).Select(f => f != default),
                    (a, b) => a && b));

        UploadMoreFilesCommand = ReactiveCommand.Create(() => { UploadProgress = 0; });

        CloseCommand = ReactiveCommand.Create(() => ShowFloatingWindow.Send(FloatingScreenType.None));
    }

    public ICommand BrowseFileCommand { get; }
    public ICommand BrowseAndUploadFileCommand { get; }
    public ICommand CopyUrlCommand { get; }
    public ICommand UploadCommand { get; }
    public ICommand CloseCommand { get; }

    // Made once the author's token has been read, as in WPF.
    [Reactive] public partial ICommand BrowseUploadsCommand { get; private set; }
    public ICommand UploadMoreFilesCommand { get; }

    [Reactive] public partial double UploadProgress { get; set; }
    [Reactive] public partial string FileUrl { get; set; }
    public FilePickerVM Picker { get; }

    private IObservable<bool> IsUploading { get; }
    public WabbajackApiState ApiToken { get; private set; }
}
