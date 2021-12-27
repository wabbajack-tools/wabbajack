using System;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.Common;
using Wabbajack.Lib.AuthorApi;
using Wabbajack.Lib.FileUploader;
using Wabbajack.Networking.Http.Interfaces;
using Wabbajack.Services.OSIntegrated.TokenProviders;

namespace Wabbajack
{
    public class AuthorFilesVM : BackNavigatingVM
    {
        private readonly ObservableAsPropertyHelper<Visibility> _isVisible;
        
        [Reactive]
        public Visibility IsVisible { get; set; };
        
        public ICommand SelectFile { get; }
        public ICommand HyperlinkCommand { get; }
        public IReactiveCommand Upload { get; }
        public IReactiveCommand ManageFiles { get; }

        [Reactive] public double UploadProgress { get; set; }
        [Reactive] public string FinalUrl { get; set; }
        public FilePickerVM Picker { get;}
        
        private Subject<bool> _isUploading = new Subject<bool>();
        private readonly WabbajackApiTokenProvider _token;
        private IObservable<bool> IsUploading { get; }

        public AuthorFilesVM(WabbajackApiTokenProvider token, SettingsVM vm) : base(vm.MWVM)
        {
            _token = token;
            IsUploading = _isUploading;
            Picker = new FilePickerVM(this);


            IsVisible = Visibility.Hidden;

            Task.Run(async () =>
            {
                var isAuthor = string.IsNullOrWhiteSpace((await _token.Get())?.AuthorKey);
                IsVisible = isAuthor ? Visibility.Visible : Visibility.Collapsed;
            });

            SelectFile = Picker.ConstructTypicalPickerCommand(IsUploading.StartWith(false).Select(u => !u));

            HyperlinkCommand = ReactiveCommand.Create(() => Clipboard.SetText(FinalUrl));

            ManageFiles = ReactiveCommand.Create(async () =>
            {
                var authorApiKey = await AuthorAPI.GetAPIKey();
                Utils.OpenWebsite(new Uri($"{Consts.WabbajackBuildServerUri}author_controls/login/{authorApiKey}"));
            });
            
            Upload = ReactiveCommand.Create(async () =>
            {
                _isUploading.OnNext(true);
                try
                {
                    using var queue = new WorkQueue();
                    var result = await (await Client.Create()).UploadFile(queue, Picker.TargetPath,
                        (msg, progress) =>
                        {
                            FinalUrl = msg;
                            UploadProgress = (double)progress;
                        });
                    FinalUrl = result.ToString();
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
