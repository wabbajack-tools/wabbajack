using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using Microsoft.Extensions.Logging;
using Wabbajack.Messages;
using ReactiveUI;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using DynamicData;
using Microsoft.WindowsAPICodePack.Dialogs;
using ReactiveUI.Fody.Helpers;
using Wabbajack.Common;
using Wabbajack.Compiler;
using Wabbajack.DTOs;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Extensions;
using Wabbajack.Installer;
using Wabbajack.Models;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;
using Wabbajack.Services.OSIntegrated;
using NexusMods.Paths.FileTree;
using System.Windows.Controls;

namespace Wabbajack
{
    public class CompilerFileManagerVM : BackNavigatingVM
    {
        private readonly DTOSerializer _dtos;
        private readonly SettingsManager _settingsManager;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<CompilerFileManagerVM> _logger;
        private readonly ResourceMonitor _resourceMonitor;
        private readonly CompilerSettingsInferencer _inferencer;
        private readonly Client _wjClient;
        
        [Reactive] public CompilerSettingsVM Settings { get; set; } = new();
        public IEnumerable<TreeViewItem> Files { get; set; }


        public CompilerFileManagerVM(ILogger<CompilerFileManagerVM> logger, DTOSerializer dtos, SettingsManager settingsManager,
            IServiceProvider serviceProvider, LogStream loggerProvider, ResourceMonitor resourceMonitor, 
            CompilerSettingsInferencer inferencer, Client wjClient) : base(logger)
        {
            _logger = logger;
            _dtos = dtos;
            _settingsManager = settingsManager;
            _serviceProvider = serviceProvider;
            _resourceMonitor = resourceMonitor;
            _inferencer = inferencer;
            _wjClient = wjClient;

            MessageBus.Current.Listen<LoadCompilerSettings>()
                .Subscribe(msg => {
                    var csVm = new CompilerSettingsVM(msg.CompilerSettings);
                    Settings = csVm;
                })
                .DisposeWith(CompositeDisposable);

            this.WhenActivated(disposables =>
            {
                var fileTree = GetDirectoryContents(new DirectoryInfo(Settings.Source.ToString()));
                Files = LoadFiles(new DirectoryInfo(Settings.Source.ToString()));
                Disposable.Create(() => { }).DisposeWith(disposables);
            });
        }

        private IEnumerable<TreeViewItem> LoadFiles(DirectoryInfo parent)
        {
            var parentTreeItem = new TreeViewItem()
            {
                Header = parent,
                IsExpanded = true,
                ItemsSource = LoadDirectoryContents(parent)
            };
            return [parentTreeItem];

        }

        private IEnumerable<TreeViewItem> LoadDirectoryContents(DirectoryInfo parent)
        {
            return parent.EnumerateDirectories()
                  .OrderBy(dir => dir.Name)
                  .Select(dir => new TreeViewItem() { Header = dir, ItemsSource = (dir.EnumerateDirectories().Any() || dir.EnumerateFiles().Any()) ? new ObservableCollection<TreeViewItem>([new TreeViewItem() { Header = "Loading..." }]) : null}).Select(item => {
                      item.Expanded += LoadingItem_Expanded;
                      return item;
                  })
                  .Concat(parent.EnumerateFiles()
                                .OrderBy(file => file.Name)
                                .Select(file => new TreeViewItem() { Header = file }));
        }

        private void LoadingItem_Expanded(object sender, System.Windows.RoutedEventArgs e)
        {
            var parent = (TreeViewItem)e.OriginalSource;
            var children = parent.ItemsSource.OfType<TreeViewItem>();
            var firstChild = children.Any() ? children.First().Header : null;
            if (firstChild != null && firstChild is string firstString && firstString == "Loading...")
                parent.ItemsSource = LoadDirectoryContents((DirectoryInfo)parent.Header);
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

        private async Task SaveSettingsFile()
        {
            if (Settings.Source == default || Settings.CompilerSettingsPath == default) return;

            await using var st = Settings.CompilerSettingsPath.Open(FileMode.Create, FileAccess.Write, FileShare.None);
            await JsonSerializer.SerializeAsync(st, Settings.ToCompilerSettings(), _dtos.Options);

            var allSavedCompilerSettings = await _settingsManager.Load<List<AbsolutePath>>(Consts.AllSavedCompilerSettingsPaths);

            // Don't simply remove Settings.CompilerSettingsPath here, because WJ sometimes likes to make default compiler settings files
            allSavedCompilerSettings.RemoveAll(path => path.Parent == Settings.Source);
            allSavedCompilerSettings.Insert(0, Settings.CompilerSettingsPath);

            await _settingsManager.Save(Consts.AllSavedCompilerSettingsPaths, allSavedCompilerSettings);
        }

        private async Task LoadLastSavedSettings()
        {
            AbsolutePath lastPath = default;
            var allSavedCompilerSettings = await _settingsManager.Load<List<AbsolutePath>>(Consts.AllSavedCompilerSettingsPaths);
            if (allSavedCompilerSettings.Any())
                lastPath = allSavedCompilerSettings[0];

            if (lastPath == default || !lastPath.FileExists() || lastPath.FileName.Extension != Ext.CompilerSettings) return;
            //ModlistLocation.TargetPath = lastPath;
        }
    }
}
