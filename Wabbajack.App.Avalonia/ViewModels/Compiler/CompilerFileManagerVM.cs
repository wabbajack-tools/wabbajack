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
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.Compiler;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Models;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.Services.OSIntegrated;
using Avalonia.Controls;
using System.Windows.Input;
using System.ComponentModel;

namespace Wabbajack;

public class CompilerFileManagerVM : BaseCompilerVM
{
    public ObservableCollection<FileTreeViewItem> Files { get; set; }

    public CompilerFileManagerVM(ILogger<CompilerFileManagerVM> logger, DTOSerializer dtos, SettingsManager settingsManager, Client wjClient) : base(dtos, settingsManager, logger, wjClient)
    {
        this.WhenActivated(disposables =>
        {
            // DirectoryExists, not just "is set": a .compiler_settings file records the Source path
            // from the machine it was authored on, so opening someone else's settings (or a moved
            // install) points here at a directory that isn't there. Enumerating it threw
            // DirectoryNotFoundException out of WhenActivated, which is unhandled and tore down the
            // whole window rather than showing an empty file tree.
            if (Settings.Source != default && Settings.Source.DirectoryExists())
            {
                try
                {
                    Files = LoadSource(new DirectoryInfo(Settings.Source.ToString()));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Could not read the compiler source folder {Source}", Settings.Source);
                }
            }
            else if (Settings.Source != default)
            {
                _logger.LogWarning("Compiler source folder {Source} does not exist; showing an empty file tree",
                    Settings.Source);
            }

            Disposable.Create(() => { }).DisposeWith(disposables);
        });
    }

    private ObservableCollection<FileTreeViewItem> LoadSource(DirectoryInfo parent)
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
              .Select(dir => new FileTreeViewItem(dir) { ItemsSource = (dir.EnumerateDirectories().Any() || dir.EnumerateFiles().Any()) ? new ObservableCollection<FileTreeViewItem>([FileTreeViewItem.Placeholder]) : null}).Select(item =>
              {
                  item.Expanded += LoadingItem_Expanded;
                  SetFileTreeViewItemProperties(item);
                  return item;
              })
              .Concat(parent.EnumerateFiles()
                            .OrderBy(file => file.Name)
                            .Select(file => {
                                var item = new FileTreeViewItem(file);
                                SetFileTreeViewItemProperties(item);
                                return item;
                            }))
              .ToList();
    }

    private void SetFileTreeViewItemProperties(FileTreeViewItem item)
    {
        var header = item.Header;
        header.PathRelativeToRoot = ((AbsolutePath)header.Info.FullName).RelativeTo(Settings.Source);

        if (Settings.NoMatchInclude.Contains(header.PathRelativeToRoot))
        {
            header.CompilerFileStates.Add(CompilerFileState.NoMatchInclude);
        }
        if (Settings.Include.Contains(header.PathRelativeToRoot))
        {
            header.CompilerFileStates.Add(CompilerFileState.Include);
        }
        if (Settings.Ignore.Contains(header.PathRelativeToRoot))
        {
            header.CompilerFileStates.Add(CompilerFileState.Ignore);
        }
        if (Settings.AlwaysEnabled.Contains(header.PathRelativeToRoot))
        {
            header.CompilerFileStates.Add(CompilerFileState.AlwaysEnabled);
        }

        header.CompilerFileState = header.CompilerFileStates.Any() ? header.CompilerFileStates.Aggregate((CompilerFileState)0, (a, b) => a | b) : null;

        SetContainedStates(header);
        header.PropertyChanged += Header_PropertyChanged;
    }

    private void SetContainedStates(FileTreeItemVM header)
    {
        if (!header.IsDirectory) return;
        header.ContainsNoMatchIncludes = Settings.NoMatchInclude.Any(p => p.InFolder(header.PathRelativeToRoot)) && !Settings.NoMatchInclude.Contains(header.PathRelativeToRoot);
        header.ContainsIncludes = Settings.Include.Any(p => p.InFolder(header.PathRelativeToRoot)) && !Settings.Include.Contains(header.PathRelativeToRoot);
        header.ContainsIgnores = Settings.Ignore.Any(p => p.InFolder(header.PathRelativeToRoot)) && !Settings.Ignore.Contains(header.PathRelativeToRoot);
        header.ContainsAlwaysEnableds = Settings.AlwaysEnabled.Any(p => p.InFolder(header.PathRelativeToRoot)) && !Settings.AlwaysEnabled.Contains(header.PathRelativeToRoot);
    }

    private async void Header_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        var updatedItem = (FileTreeItemVM)sender;
        if(e.PropertyName == nameof(FileTreeItemVM.CompilerFileState))
        {
            Settings.NoMatchInclude.Remove(updatedItem.PathRelativeToRoot);
            Settings.Include.Remove(updatedItem.PathRelativeToRoot);
            Settings.Ignore.Remove(updatedItem.PathRelativeToRoot);
            Settings.AlwaysEnabled.Remove(updatedItem.PathRelativeToRoot);

            if (updatedItem.CompilerFileState.HasValue)
            {
                if (updatedItem.CompilerFileState.Value.HasFlag(CompilerFileState.NoMatchInclude))
                {
                    Settings.NoMatchInclude.Add(updatedItem.PathRelativeToRoot);
                }
                if (updatedItem.CompilerFileState.Value.HasFlag(CompilerFileState.Include))
                {
                    Settings.Include.Add(updatedItem.PathRelativeToRoot);
                }
                if (updatedItem.CompilerFileState.Value.HasFlag(CompilerFileState.Ignore))
                {
                    Settings.Ignore.Add(updatedItem.PathRelativeToRoot);
                }
                if (updatedItem.CompilerFileState.Value.HasFlag(CompilerFileState.AlwaysEnabled))
                {
                    Settings.AlwaysEnabled.Add(updatedItem.PathRelativeToRoot);
                }
            }

            // Update contained states of parents upon changing compiler state on child (ContainsIgnores, ContainsIncludes)
            if (updatedItem.PathRelativeToRoot.Depth > 1)
            {
                IEnumerable<FileTreeViewItem> files = Files.First().ItemsSource.Cast<FileTreeViewItem>();
                for (int i = 0; i < updatedItem.PathRelativeToRoot.Depth - 1; i++)
                {
                    var currPathPart = updatedItem.PathRelativeToRoot.Parts[i];
                    foreach (var file in files)
                    {
                        if (file.Header.ToString() == currPathPart)
                        {
                            SetContainedStates(file.Header);
                            files = file.ItemsSource.Cast<FileTreeViewItem>();
                            break;
                        }
                    }
                }
            }

            await SaveSettings();
        }
    }

    private void LoadingItem_Expanded(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var parent = (FileTreeViewItem)e.Source;
        if (parent?.ItemsSource == null) return;
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
        try
        {
            var directories = dir.EnumerateDirectories();
            var items = dir.EnumerateFiles();
            return directories.OrderBy(x => x.Name).Concat<FileSystemInfo>(items.OrderBy(y => y.Name));
        }
        catch(Exception ex)
        {
            _logger.LogError("While loading compiler settings path for directory {dir}: {ex}", dir.FullName, ex.ToString());
            throw;
        }
    }
}
