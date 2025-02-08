using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using System.Web;
using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.DTOs.Logins;
using Wabbajack.Messages;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.RateLimiter;
using Wabbajack.Services.OSIntegrated.TokenProviders;

namespace Wabbajack;

public class FileUploadVM : ViewModel
{

    private readonly ILogger<FileUploadVM> _logger;
    private readonly WabbajackApiTokenProvider _tokenProvider;
    private readonly Client _wjClient;

    public ICommand BrowseFileCommand { get; }
    public ICommand BrowseAndUploadFileCommand { get; }
    public ICommand CopyUrlCommand { get; }
    public ICommand UploadCommand { get; }
    public ICommand CloseCommand { get; }
    public ICommand BrowseUploadsCommand { get; private set; }
    public ICommand UploadMoreFilesCommand { get; private set; }

    [Reactive] public double UploadProgress { get; set; }
    [Reactive] public string FileUrl { get; set; }
    public FilePickerVM Picker { get;}
    
    private Subject<bool> _isUploading = new();
    private IObservable<bool> IsUploading { get; }
    public WabbajackApiState ApiToken { get; private set; }

    public FileUploadVM(ILogger<FileUploadVM> logger, WabbajackApiTokenProvider tokenProvider, Client wjClient, SettingsVM vm)
    {
        _logger = logger;
        _tokenProvider = tokenProvider;
        _wjClient = wjClient;
        IsUploading = _isUploading;
        Picker = new FilePickerVM(this);

        Task.Run(async () =>
        {
            ApiToken = await _tokenProvider.Get();
            BrowseUploadsCommand = ReactiveCommand.Create(async () =>
            {
                var authorApiKey = ApiToken?.AuthorKey;
                UIUtils.OpenWebsite(new Uri($"{Consts.WabbajackBuildServerUri}author_controls/login/{authorApiKey}"));
            });
        });

        BrowseFileCommand = Picker.ConstructTypicalPickerCommand(IsUploading.StartWith(false).Select(u => !u));
        BrowseAndUploadFileCommand = ReactiveCommand.Create(() => {
            BrowseFileCommand.Execute(null);
            UploadCommand.Execute(null);
        });

        CopyUrlCommand = ReactiveCommand.Create(() => Clipboard.SetText(FileUrl));

        UploadCommand = ReactiveCommand.Create(async () =>
        {
            _isUploading.OnNext(true);
            try
            {
                var (progress, task) = await _wjClient.UploadAuthorFile(Picker.TargetPath);

                var disposable = progress.Subscribe(m =>
                {
                    FileUrl = m.Message;
                    if(m.PercentDone != Percent.Zero) UploadProgress = (double)m.PercentDone;
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

        UploadMoreFilesCommand = ReactiveCommand.Create(() =>
        {
            UploadProgress = 0;
        });

        CloseCommand = ReactiveCommand.Create(() => ShowFloatingWindow.Send(FloatingScreenType.None));
    }

}
