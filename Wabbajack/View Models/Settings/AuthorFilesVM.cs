using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Windows;
using System.Windows.Input;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.Common;
using Wabbajack.Lib.AuthorApi;
using Wabbajack.Lib.FileUploader;

namespace Wabbajack
{
    public class AuthorFilesVM : BackNavigatingVM
    {
        private readonly ObservableAsPropertyHelper<Visibility> _isVisible;
        public Visibility IsVisible => _isVisible.Value;
        

        private readonly ObservableAsPropertyHelper<string> _selectedFile;
      
        public ICommand SelectFile { get; }
        public ICommand HyperlinkCommand { get; }
        public IReactiveCommand Upload { get; }

        [Reactive] public double UploadProgress { get; set; }
        [Reactive] public string FinalUrl { get; set; }
        
        private WorkQueue Queue = new WorkQueue(1);
        
        public FilePickerVM Picker { get;}
        
        private Subject<bool> _isUploading = new Subject<bool>();
        private IObservable<bool> IsUploading { get; }

        public AuthorFilesVM(SettingsVM vm) : base(vm.MWVM)
        {
            IsUploading = _isUploading;
            Picker = new FilePickerVM(this);

            _isVisible = AuthorAPI.HaveAuthorAPIKey.Select(h => h ? Visibility.Visible : Visibility.Collapsed)
                .ToProperty(this, x => x.IsVisible);

            SelectFile = Picker.ConstructTypicalPickerCommand(IsUploading.StartWith(false).Select(u => !u));

            HyperlinkCommand = ReactiveCommand.Create(() => Clipboard.SetText(FinalUrl));
            
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
                .CombineLatest(Picker.WhenAnyValue(t => t.TargetPath).Select(f => f != null),
                (a, b) => a && b));
        }
    }
}
