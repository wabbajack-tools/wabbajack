using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;

using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using Alphaleonis.Win32.Filesystem;
using Microsoft.WindowsAPICodePack.Shell.PropertySystem;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.Common;
using Wabbajack.Lib.FileUploader;
using Wabbajack.Lib.GraphQL;
using Wabbajack.Lib.GraphQL.DTOs;
using File = System.IO.File;

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
                    FinalUrl = await AuthorAPI.UploadFile(Picker.TargetPath,
                        progress => UploadProgress = progress);
                }
                catch (Exception ex)
                {
                    FinalUrl = ex.ToString();
                }
                finally
                {
                    _isUploading.OnNext(false);
                }
            }, IsUploading.StartWith(false).Select(u => !u)
                .CombineLatest(Picker.WhenAnyValue(t => t.TargetPath).Select(f => f != null),
                (a, b) => a && b));
        }
    }
}
