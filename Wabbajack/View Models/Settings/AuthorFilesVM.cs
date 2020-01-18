using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;

using System.Windows;
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
        public Visibility IsVisible { get; }
        
        [Reactive]
        public string SelectedFile { get; set; }
        
        public IReactiveCommand SelectFile { get; }
        public IReactiveCommand Upload { get; }
        
        [Reactive]
        public double UploadProgress { get; set; }
        
        private WorkQueue Queue = new WorkQueue(1);
        
        public AuthorFilesVM(SettingsVM vm) : base(vm.MWVM)
        {
            var sub = new Subject<double>();
            Queue.Status.Select(s => (double)s.ProgressPercent).Subscribe(v =>
            {
                UploadProgress = v;
            });
            IsVisible = AuthorAPI.HasAPIKey ? Visibility.Visible : Visibility.Collapsed;
            
            SelectFile = ReactiveCommand.Create(() =>
            {
                var fod = UIUtils.OpenFileDialog("*|*");
                if (fod != null)
                    SelectedFile = fod;
            });

            Upload = ReactiveCommand.Create(async () =>
            {
                SelectedFile = await GraphQLService.UploadFile(Queue, SelectedFile);
            });
        }
    }
}
