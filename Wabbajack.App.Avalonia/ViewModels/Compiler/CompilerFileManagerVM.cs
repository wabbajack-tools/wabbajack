using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.Paths;
using Wabbajack.Services.OSIntegrated;

namespace Wabbajack.App.Avalonia.ViewModels.Compiler;

/// <summary>
/// The file tree beside the compiler's details: the list's MO2 folder, where each file or folder can be marked
/// No Match Include, Include, Ignore or Always Enabled. A change is written to this view model's own settings
/// and saved straight away, as in WPF.
/// </summary>
public partial class CompilerFileManagerVM : BaseCompilerVM
{
    public CompilerFileManagerVM(ILogger<CompilerFileManagerVM> logger, DTOSerializer dtos,
        SettingsManager settingsManager, Client wjClient) : base(dtos, settingsManager, logger, wjClient)
    {
        this.WhenActivated(disposables =>
        {
            if (Settings.Source != default)
                Files = LoadSource(new DirectoryInfo(Settings.Source.ToString()));

            Disposable.Create(() => { }).DisposeWith(disposables);
        });
    }

    [Reactive] public partial ObservableCollection<FileTreeItemVM> Files { get; set; }

    private ObservableCollection<FileTreeItemVM> LoadSource(DirectoryInfo parent)
    {
        var root = new FileTreeItemVM(parent) { HasItems = true };
        foreach (var child in LoadDirectoryContents(parent)) root.Children.Add(child);
        root.IsExpanded = true;
        return [root];
    }

    private List<FileTreeItemVM> LoadDirectoryContents(DirectoryInfo parent)
    {
        return parent.EnumerateDirectories()
            .OrderBy(dir => dir.Name)
            .Select(dir =>
            {
                var item = new FileTreeItemVM(dir)
                {
                    HasItems = dir.EnumerateDirectories().Any() || dir.EnumerateFiles().Any()
                };
                item.LoadChildrenOnExpand(p => LoadDirectoryContents((DirectoryInfo)p.Info));
                SetFileTreeItemProperties(item);
                return item;
            })
            .Concat(parent.EnumerateFiles()
                .OrderBy(file => file.Name)
                .Select(file =>
                {
                    var item = new FileTreeItemVM(file);
                    SetFileTreeItemProperties(item);
                    return item;
                }))
            .ToList();
    }

    private void SetFileTreeItemProperties(FileTreeItemVM item)
    {
        item.PathRelativeToRoot = ((AbsolutePath)item.Info.FullName).RelativeTo(Settings.Source);

        if (Settings.NoMatchInclude.Contains(item.PathRelativeToRoot))
            item.CompilerFileStates.Add(CompilerFileState.NoMatchInclude);
        if (Settings.Include.Contains(item.PathRelativeToRoot))
            item.CompilerFileStates.Add(CompilerFileState.Include);
        if (Settings.Ignore.Contains(item.PathRelativeToRoot))
            item.CompilerFileStates.Add(CompilerFileState.Ignore);
        if (Settings.AlwaysEnabled.Contains(item.PathRelativeToRoot))
            item.CompilerFileStates.Add(CompilerFileState.AlwaysEnabled);

        item.CompilerFileState = item.CompilerFileStates.Any()
            ? item.CompilerFileStates.Aggregate((CompilerFileState)0, (a, b) => a | b)
            : null;

        SetContainedStates(item);
        item.PropertyChanged += Item_PropertyChanged;
    }

    private void SetContainedStates(FileTreeItemVM item)
    {
        if (!item.IsDirectory) return;
        var path = item.PathRelativeToRoot;
        item.ContainsNoMatchIncludes = Settings.NoMatchInclude.Any(p => p.InFolder(path)) && !Settings.NoMatchInclude.Contains(path);
        item.ContainsIncludes = Settings.Include.Any(p => p.InFolder(path)) && !Settings.Include.Contains(path);
        item.ContainsIgnores = Settings.Ignore.Any(p => p.InFolder(path)) && !Settings.Ignore.Contains(path);
        item.ContainsAlwaysEnableds = Settings.AlwaysEnabled.Any(p => p.InFolder(path)) && !Settings.AlwaysEnabled.Contains(path);
    }

    private async void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(FileTreeItemVM.CompilerFileState)) return;
        var updatedItem = (FileTreeItemVM)sender!;

        Settings.NoMatchInclude.Remove(updatedItem.PathRelativeToRoot);
        Settings.Include.Remove(updatedItem.PathRelativeToRoot);
        Settings.Ignore.Remove(updatedItem.PathRelativeToRoot);
        Settings.AlwaysEnabled.Remove(updatedItem.PathRelativeToRoot);

        if (updatedItem.CompilerFileState is { } state)
        {
            if (state.HasFlag(CompilerFileState.NoMatchInclude)) Settings.NoMatchInclude.Add(updatedItem.PathRelativeToRoot);
            if (state.HasFlag(CompilerFileState.Include)) Settings.Include.Add(updatedItem.PathRelativeToRoot);
            if (state.HasFlag(CompilerFileState.Ignore)) Settings.Ignore.Add(updatedItem.PathRelativeToRoot);
            if (state.HasFlag(CompilerFileState.AlwaysEnabled)) Settings.AlwaysEnabled.Add(updatedItem.PathRelativeToRoot);
        }

        // Parents' contained states (the stripes) follow a change on a child.
        if (updatedItem.PathRelativeToRoot.Depth > 1)
        {
            IEnumerable<FileTreeItemVM> files = Files.First().Children;
            for (var i = 0; i < updatedItem.PathRelativeToRoot.Depth - 1; i++)
            {
                var currPathPart = updatedItem.PathRelativeToRoot.Parts[i];
                foreach (var file in files)
                {
                    if (file.ToString() != currPathPart) continue;
                    SetContainedStates(file);
                    files = file.Children;
                    break;
                }
            }
        }

        await SaveSettings();
    }
}
