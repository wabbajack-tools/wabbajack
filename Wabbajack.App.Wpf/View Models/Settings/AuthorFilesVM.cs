using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.Services.OSIntegrated.TokenProviders;

namespace Wabbajack.View_Models.Settings
{
    public class AuthorFilesVM : BackNavigatingVM
    {
        private readonly ObservableAsPropertyHelper<Visibility> _isVisible;
        
        [Reactive]
        public Visibility IsVisible { get; set; }
        
        public ICommand SelectFile { get; }
        public ICommand HyperlinkCommand { get; }
        public IReactiveCommand Upload { get; }
        public IReactiveCommand ManageFiles { get; }

        [Reactive] public double UploadProgress { get; set; }
        [Reactive] public string FinalUrl { get; set; }
        public FilePickerVM Picker { get;}
        
        private Subject<bool> _isUploading = new();
        private readonly WabbajackApiTokenProvider _token;
        private readonly Client _wjClient;
        private IObservable<bool> IsUploading { get; }

        public AuthorFilesVM(ILogger<AuthorFilesVM> logger, WabbajackApiTokenProvider token, Client wjClient, SettingsVM vm) : base(logger)
        {
            _token = token;
            _wjClient = wjClient;
            IsUploading = _isUploading;
            Picker = new FilePickerVM(this);


            IsVisible = Visibility.Hidden;

            Task.Run(async () =>
            {
                var isAuthor = !string.IsNullOrWhiteSpace((await _token.Get())?.AuthorKey);
                IsVisible = isAuthor ? Visibility.Visible : Visibility.Collapsed;
            });

            SelectFile = Picker.ConstructTypicalPickerCommand(IsUploading.StartWith(false).Select(u => !u));

            HyperlinkCommand = ReactiveCommand.Create(() => Clipboard.SetText(FinalUrl));

            ManageFiles = ReactiveCommand.Create(async () =>
            {
                var authorApiKey = (await token.Get())!.AuthorKey;
                UIUtils.OpenWebsite(new Uri($"{Consts.WabbajackBuildServerUri}author_controls/login/{authorApiKey}"));
            });
            
            Upload = ReactiveCommand.Create(async () =>
            {
                _isUploading.OnNext(true);
                try
                {
                    var (progress, task) = await _wjClient.UploadAuthorFile(Picker.TargetPath);

                    var disposable = progress.Subscribe(m =>
                    {
                        FinalUrl = m.Message;
                        UploadProgress = (double)m.PercentDone;
                    });

                    var final = await task;
                    disposable.Dispose();
                    FinalUrl = final.ToString();
                }
                catch (Exception ex)
                {
                    FinalUrl = ex.ToString();
                }
                finally
                {
                    FinalUrl = FinalUrl.Replace(" ", "%20");
                    _isUploading.OnNext(false);
                }
            }, IsUploading.StartWith(false).Select(u => !u)
                .CombineLatest(Picker.WhenAnyValue(t => t.TargetPath).Select(f => f != default),
                (a, b) => a && b));
        }

    }
}
