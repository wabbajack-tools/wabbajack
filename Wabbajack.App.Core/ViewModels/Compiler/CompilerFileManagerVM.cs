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
using Wabbajack.Services.OSIntegrated;

namespace Wabbajack;

public class CompilerFileManagerVM : BaseCompilerVM
{
    public ObservableCollection<FileTreeItemVM> Files { get; set; }

    public CompilerFileManagerVM(ILogger<CompilerFileManagerVM> logger, DTOSerializer dtos, SettingsManager settingsManager, Client wjClient) : base(dtos, settingsManager, logger, wjClient)
    {
        this.WhenActivated(disposables =>
        {
            if (Settings.Source != default)
            {
                Files = LoadSource(new DirectoryInfo(Settings.Source.ToString()));
            }

            Disposable.Create(() => { }).DisposeWith(disposables);
        });
    }

    private ObservableCollection<FileTreeItemVM> LoadSource(DirectoryInfo parent)
    {
        var root = new FileTreeItemVM(parent)
        {
            IsExpanded = true
        };

        foreach (var child in LoadDirectoryContents(parent))
        {
            root.Children.Add(child);
        }
        root.ChildrenLoaded = true;

        return new ObservableCollection<FileTreeItemVM> { root };
    }

    /// <summary>
    /// Builds the immediate children (subdirectories then files) of <paramref name="parent"/> as
    /// <see cref="FileTreeItemVM"/> nodes. Directory nodes that have any on-disk children get a single
    /// placeholder child so the expander affordance shows; their real contents are realized lazily when
    /// the node is expanded (see <see cref="WireDirectoryNode"/>).
    /// </summary>
    private IEnumerable<FileTreeItemVM> LoadDirectoryContents(DirectoryInfo parent)
    {
        var nodes = new List<FileTreeItemVM>();

        foreach (var dir in parent.EnumerateDirectories().OrderBy(dir => dir.Name))
        {
            var node = new FileTreeItemVM(dir);
            if (dir.EnumerateDirectories().Any() || dir.EnumerateFiles().Any())
            {
                node.Children.Add(CreatePlaceholder(dir));
            }
            SetItemProperties(node);
            WireDirectoryNode(node);
            nodes.Add(node);
        }

        foreach (var file in parent.EnumerateFiles().OrderBy(file => file.Name))
        {
            var node = new FileTreeItemVM(file);
            SetItemProperties(node);
            nodes.Add(node);
        }

        return nodes;
    }

    /// <summary>
    /// Creates the single sentinel placeholder child used so an unrealized directory still shows its
    /// expander. It is replaced by the real contents on first expansion.
    /// </summary>
    private static FileTreeItemVM CreatePlaceholder(DirectoryInfo dir) => new(dir) { IsPlaceholder = true };

    /// <summary>
    /// Wires lazy-load on expansion for a directory node: when <see cref="FileTreeItemVM.IsExpanded"/>
    /// flips true and the children haven't been realized yet, clear the placeholder and populate from disk.
    /// </summary>
    private void WireDirectoryNode(FileTreeItemVM node)
    {
        if (!node.IsDirectory) return;

        node.WhenAnyValue(x => x.IsExpanded)
            .Where(expanded => expanded && !node.ChildrenLoaded)
            .Subscribe(_ =>
            {
                node.Children.Clear();
                foreach (var child in LoadDirectoryContents((DirectoryInfo)node.Info))
                {
                    node.Children.Add(child);
                }
                node.ChildrenLoaded = true;
            })
            .DisposeWith(CompositeDisposable);
    }

    private void SetItemProperties(FileTreeItemVM item)
    {
        item.PathRelativeToRoot = ((AbsolutePath)item.Info.FullName).RelativeTo(Settings.Source);

        if (Settings.NoMatchInclude.Contains(item.PathRelativeToRoot))
        {
            item.CompilerFileStates.Add(CompilerFileState.NoMatchInclude);
        }
        if (Settings.Include.Contains(item.PathRelativeToRoot))
        {
            item.CompilerFileStates.Add(CompilerFileState.Include);
        }
        if (Settings.Ignore.Contains(item.PathRelativeToRoot))
        {
            item.CompilerFileStates.Add(CompilerFileState.Ignore);
        }
        if (Settings.AlwaysEnabled.Contains(item.PathRelativeToRoot))
        {
            item.CompilerFileStates.Add(CompilerFileState.AlwaysEnabled);
        }

        item.CompilerFileState = item.CompilerFileStates.Any() ? item.CompilerFileStates.Aggregate((CompilerFileState)0, (a, b) => a | b) : null;

        SetContainedStates(item);

        item.WhenAnyValue(x => x.CompilerFileState)
            .Skip(1)
            .Subscribe(_ => OnCompilerFileStateChanged(item))
            .DisposeWith(CompositeDisposable);
    }

    private void SetContainedStates(FileTreeItemVM item)
    {
        if (!item.IsDirectory) return;
        item.ContainsNoMatchIncludes = Settings.NoMatchInclude.Any(p => p.InFolder(item.PathRelativeToRoot)) && !Settings.NoMatchInclude.Contains(item.PathRelativeToRoot);
        item.ContainsIncludes = Settings.Include.Any(p => p.InFolder(item.PathRelativeToRoot)) && !Settings.Include.Contains(item.PathRelativeToRoot);
        item.ContainsIgnores = Settings.Ignore.Any(p => p.InFolder(item.PathRelativeToRoot)) && !Settings.Ignore.Contains(item.PathRelativeToRoot);
        item.ContainsAlwaysEnableds = Settings.AlwaysEnabled.Any(p => p.InFolder(item.PathRelativeToRoot)) && !Settings.AlwaysEnabled.Contains(item.PathRelativeToRoot);
    }

    private async void OnCompilerFileStateChanged(FileTreeItemVM updatedItem)
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
        if (updatedItem.PathRelativeToRoot.Depth > 1 && Files != null && Files.Any())
        {
            IEnumerable<FileTreeItemVM> files = Files.First().Children;
            for (int i = 0; i < updatedItem.PathRelativeToRoot.Depth - 1; i++)
            {
                var currPathPart = updatedItem.PathRelativeToRoot.Parts[i];
                foreach (var file in files)
                {
                    if (file.ToString() == currPathPart)
                    {
                        SetContainedStates(file);
                        files = file.Children;
                        break;
                    }
                }
            }
        }

        await SaveSettings();
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
