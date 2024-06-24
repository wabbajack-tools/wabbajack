using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Wabbajack.Messages;
using ReactiveUI;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ReactiveUI.Fody.Helpers;
using Wabbajack.Common;
using Wabbajack.Compiler;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Models;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.Services.OSIntegrated;
using System.Windows.Controls;
using FluentIcons.Common;
using System.Windows.Input;
using System.ComponentModel;

namespace Wabbajack
{
    public enum CompilerFileState
    {
        [Description("Auto Match")]
        AutoMatch,
        [Description("No Match Include")]
        NoMatchInclude,
        [Description("Force Include")]
        Include,
        [Description("Force Ignore")]
        Ignore,
        [Description("Always Enabled")]
        AlwaysEnabled
    }
    public class FileTreeViewItem : TreeViewItem
    {
        public FileTreeViewItem(DirectoryInfo dir)
        {
            base.Header = new FileTreeItemVM(dir);
        }
        public FileTreeViewItem(FileInfo file)
        {
            base.Header = new FileTreeItemVM(file);
        }
        public new FileTreeItemVM Header => base.Header as FileTreeItemVM;
        public static FileTreeViewItem Placeholder => default;
    }
    public class FileTreeItemVM : ReactiveObject, IDisposable
    {
        private readonly CompositeDisposable _disposable = new();
        public FileSystemInfo Info { get; set; }
        public bool IsDirectory { get; set; }
        public Symbol Symbol { get; set; }
        [Reactive] public CompilerFileState CompilerFileState { get; set; }

        public RelativePath PathRelativeToRoot { get; set; }
        [Reactive] public bool SpecialFileState { get; set; }
        public FileTreeItemVM(DirectoryInfo info)
        {
            Info = info;
            IsDirectory = true;
            Symbol = Symbol.Folder;

            this.WhenAnyValue(ftvivm => ftvivm.CompilerFileState)
                .Subscribe(cfs => SpecialFileState = cfs != CompilerFileState.AutoMatch)
                .DisposeWith(_disposable);
        }
        public FileTreeItemVM(FileInfo info)
        {
            Info = info;
            Symbol = info.Extension.ToLower() switch {
                ".7z" or ".zip" or ".rar" or ".bsa" or ".ba2" or ".wabbajack" or ".tar" or ".tar.gz" => Symbol.Archive,
                ".toml" or ".ini" or ".cfg" or ".json" or ".yaml" or ".xml" or ".yml" or ".meta" => Symbol.DocumentSettings,
                ".txt" or ".md" or ".compiler_settings" or ".log" => Symbol.DocumentText,
                ".dds" or ".jpg" or ".png" or ".webp" or ".svg" or ".xnb" => Symbol.DocumentImage,
                ".hkx" => Symbol.DocumentPerson,
                ".nif" or ".btr" => Symbol.DocumentCube,
                ".mp3" or ".wav" or ".fuz" => Symbol.DocumentCatchUp,
                ".js" => Symbol.DocumentJavascript,
                ".java" => Symbol.DocumentJava,
                ".pdf" => Symbol.DocumentPdf,
                ".lua" or ".py" or ".bat" or ".reds" or ".psc" => Symbol.Receipt,
                ".exe" => Symbol.ReceiptPlay,
                ".esp" or ".esl" or ".esm" or ".archive" => Symbol.DocumentTable,
                _ => Symbol.Document
            };
            SpecialFileState = CompilerFileState != CompilerFileState.AutoMatch;

            this.WhenAnyValue(ftvivm => ftvivm.CompilerFileState)
                .Subscribe(cfs => SpecialFileState = cfs != CompilerFileState.AutoMatch)
                .DisposeWith(_disposable);
        }
        public override string ToString() => Info.Name;
        public void Dispose()
        {
            GC.SuppressFinalize(this);
            _disposable.Dispose();
        }
    }
    public class CompilerFileManagerVM : BaseCompilerVM
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ResourceMonitor _resourceMonitor;
        private readonly CompilerSettingsInferencer _inferencer;
        
        public ObservableCollection<FileTreeViewItem> Files { get; set; }
        public ICommand PrevCommand { get; set; }

        public CompilerFileManagerVM(ILogger<CompilerFileManagerVM> logger, DTOSerializer dtos, SettingsManager settingsManager,
            IServiceProvider serviceProvider, LogStream loggerProvider, ResourceMonitor resourceMonitor, 
            CompilerSettingsInferencer inferencer, Client wjClient) : base(dtos, settingsManager, logger, wjClient)
        {
            _serviceProvider = serviceProvider;
            _resourceMonitor = resourceMonitor;
            _inferencer = inferencer;

            MessageBus.Current.Listen<LoadCompilerSettings>()
                .Subscribe(msg => {
                    var csVm = new CompilerSettingsVM(msg.CompilerSettings);
                    Settings = csVm;
                })
                .DisposeWith(CompositeDisposable);

            PrevCommand = ReactiveCommand.Create(PrevPage);
            this.WhenActivated(disposables =>
            {
                var fileTree = GetDirectoryContents(new DirectoryInfo(Settings.Source.ToString()));
                Files = LoadFiles(new DirectoryInfo(Settings.Source.ToString()));

                Disposable.Create(() => { }).DisposeWith(disposables);
            });
        }

        private void PrevPage()
        {
            NavigateToGlobal.Send(ScreenType.CompilerDetails);
            LoadCompilerSettings.Send(Settings.ToCompilerSettings());
        }

        private ObservableCollection<FileTreeViewItem> LoadFiles(DirectoryInfo parent)
        {
            var parentTreeItem = new FileTreeViewItem(parent)
            {
                IsExpanded = true,
                ItemsSource = LoadDirectoryContents(parent),
            };
            return [parentTreeItem];

        }

        private IEnumerable<TreeViewItem> LoadDirectoryContents(DirectoryInfo parent)
        {
            return parent.EnumerateDirectories()
                  .OrderBy(dir => dir.Name)
                  .Select(dir => new FileTreeViewItem(dir) { ItemsSource = (dir.EnumerateDirectories().Any() || dir.EnumerateFiles().Any()) ? new ObservableCollection<FileTreeViewItem>([FileTreeViewItem.Placeholder]) : null}).Select(item => {
                      item.Expanded += LoadingItem_Expanded;
                      var header = item.Header;
                      header.PathRelativeToRoot = ((AbsolutePath)header.Info.FullName).RelativeTo(Settings.Source);
                      if (Settings.NoMatchInclude.Contains(header.PathRelativeToRoot)) { header.CompilerFileState = CompilerFileState.NoMatchInclude; }
                      else if(Settings.Include.Contains(header.PathRelativeToRoot)) { header.CompilerFileState = CompilerFileState.Include; }
                      else if(Settings.Ignore.Contains(header.PathRelativeToRoot)) { header.CompilerFileState = CompilerFileState.Ignore; }
                      else if(Settings.AlwaysEnabled.Contains(header.PathRelativeToRoot)) { header.CompilerFileState = CompilerFileState.AlwaysEnabled; }
                      header.SpecialFileState = header.CompilerFileState != CompilerFileState.AutoMatch;
                      while(!header.SpecialFileState)
                      {
                          header.SpecialFileState = Settings.NoMatchInclude.Any(p => header.PathRelativeToRoot.InFolder(p));
                          header.SpecialFileState = Settings.Include.Any(p => header.PathRelativeToRoot.InFolder(p));
                          header.SpecialFileState = Settings.Ignore.Any(p => header.PathRelativeToRoot.InFolder(p));
                          header.SpecialFileState = Settings.AlwaysEnabled.Any(p => header.PathRelativeToRoot.InFolder(p));
                          break;
                      }
                      header.PropertyChanged += Header_PropertyChanged;
                      return item;
                  })
                  .Concat(parent.EnumerateFiles()
                                .OrderBy(file => file.Name)
                                .Select(file => new FileTreeViewItem(file)));
        }

        private async void Header_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            var updatedItem = (FileTreeItemVM)sender;
            if(e.PropertyName == nameof(FileTreeItemVM.SpecialFileState))
            {
                IEnumerable<FileTreeViewItem> currentEnumerable = null;
                for (int i = 0; i < updatedItem.PathRelativeToRoot.Depth - 1; i++)
                {
                    if (currentEnumerable == null)
                        currentEnumerable = ((IEnumerable<FileTreeViewItem>)Files.ElementAt(0).ItemsSource);

                    var currentItem = currentEnumerable.First(x => x.Header.IsDirectory && updatedItem.PathRelativeToRoot.Parts[i] == x.Header.Info.Name);
                    currentItem.Header.SpecialFileState = updatedItem.CompilerFileState != CompilerFileState.AutoMatch;
                    currentEnumerable = (IEnumerable<FileTreeViewItem>)currentItem.ItemsSource;
                }
            }
            else if(e.PropertyName == nameof(FileTreeItemVM.CompilerFileState))
            {
                Settings.NoMatchInclude.Remove(updatedItem.PathRelativeToRoot);
                Settings.Include.Remove(updatedItem.PathRelativeToRoot);
                Settings.Ignore.Remove(updatedItem.PathRelativeToRoot);
                Settings.AlwaysEnabled.Remove(updatedItem.PathRelativeToRoot);

                switch(updatedItem.CompilerFileState)
                {
                    case CompilerFileState.NoMatchInclude:
                        Settings.NoMatchInclude.Add(updatedItem.PathRelativeToRoot);
                        break;
                    case CompilerFileState.Include:
                        Settings.Include.Add(updatedItem.PathRelativeToRoot);
                        break;
                    case CompilerFileState.Ignore:
                        Settings.Ignore.Add(updatedItem.PathRelativeToRoot);
                        break;
                    case CompilerFileState.AlwaysEnabled:
                        Settings.AlwaysEnabled.Add(updatedItem.PathRelativeToRoot);
                        break;
                };
                await SaveSettings();
            }
        }

        private void LoadingItem_Expanded(object sender, System.Windows.RoutedEventArgs e)
        {
            var parent = (FileTreeViewItem)e.OriginalSource;
            foreach(var child in parent.ItemsSource)
            {
                if (child == FileTreeViewItem.Placeholder)
                {
                    parent.ItemsSource = LoadDirectoryContents((DirectoryInfo)parent.Header.Info);
                    break;
                }
                break;
            }
        }

        private IEnumerable<FileSystemInfo> GetDirectoryContents(DirectoryInfo dir)
        {
            var directories = dir.EnumerateDirectories();
            var items = dir.EnumerateFiles();
            return directories.OrderBy(x => x.Name).Concat<FileSystemInfo>(items.OrderBy(y => y.Name));
        }

        private async Task NextPage()
        {
            NavigateToGlobal.Send(ScreenType.CompilerFileManager);
            LoadCompilerSettings.Send(Settings.ToCompilerSettings());
        }
    }
}
