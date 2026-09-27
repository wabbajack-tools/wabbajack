using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using FluentIcons.Common;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Wabbajack.Paths;

namespace Wabbajack.App.Avalonia.ViewModels.Compiler;

[Flags]
public enum CompilerFileState : uint
{
    [Description("No Match Include")] NoMatchInclude = 1,
    [Description("Include")] Include = 2,
    [Description("Ignore")] Ignore = 4,
    [Description("Always Enabled")] AlwaysEnabled = 8
}

/// <summary>
/// One file or folder in the compiler's file tree, as the WPF FileTreeItemVM: its icon, the compiler states the
/// author gave it, and whether anything beneath it has one. In WPF the tree's items were TreeViewItems made in
/// the view model with this as their header; here the tree binds to these directly, so a folder also carries its
/// children and whether it is expanded, and loads the children the first time it is.
/// </summary>
public partial class FileTreeItemVM : ReactiveObject, IDisposable
{
    private readonly CompositeDisposable _disposable = new();
    private Func<FileTreeItemVM, IEnumerable<FileTreeItemVM>>? _loadChildren;

    public FileTreeItemVM(DirectoryInfo info)
    {
        Info = info;
        IsDirectory = true;
        Symbol = Symbol.Folder;
        WatchStates();
    }

    public FileTreeItemVM(FileInfo info)
    {
        Info = info;
        Symbol = info.Extension.ToLower() switch
        {
            ".7z" or ".zip" or ".rar" or ".bsa" or ".ba2" or ".wabbajack" or ".tar" or ".tar.gz" => Symbol.Archive,
            ".toml" or ".ini" or ".cfg" or ".json" or ".yaml" or ".xml" or ".yml" or ".meta" => Symbol.DocumentSettings,
            ".txt" or ".md" or ".compiler_settings" or ".log" => Symbol.DocumentText,
            ".dds" or ".jpg" or ".png" or ".webp" or ".svg" or ".xnb" => Symbol.DocumentImage,
            ".hkx" => Symbol.DocumentPerson,
            ".nif" or ".btr" => Symbol.DocumentCube,
            ".mp3" or ".wav" or ".fuz" => Symbol.DocumentCatchUp,
            ".js" => Symbol.DocumentJavaScript,
            ".java" => Symbol.DocumentJava,
            ".pdf" => Symbol.DocumentPdf,
            ".lua" or ".py" or ".bat" or ".reds" or ".psc" => Symbol.Receipt,
            ".exe" => Symbol.ReceiptPlay,
            ".esp" or ".esl" or ".esm" or ".archive" => Symbol.DocumentTable,
            _ => Symbol.Document
        };
        WatchStates();
    }

    /// <summary>What the states box offers, in declaration order: WPF's EnumToItemsSource.</summary>
    public static IReadOnlyList<object> AllStates { get; } = Enum.GetValues<CompilerFileState>().Cast<object>().ToList();

    public FileSystemInfo Info { get; set; }
    public bool IsDirectory { get; set; }
    public Symbol Symbol { get; set; }
    public string Name => Info.Name;

    [Reactive] public partial CompilerFileState? CompilerFileState { get; set; }
    public ObservableCollection<CompilerFileState> CompilerFileStates { get; } = new();

    /// <summary>Whether the author has given this item any state; the tree shows its states box while it has.</summary>
    [Reactive] public partial bool HasStates { get; private set; }

    public RelativePath PathRelativeToRoot { get; set; }
    [Reactive] public partial bool ContainsNoMatchIncludes { get; set; }
    [Reactive] public partial bool ContainsIncludes { get; set; }
    [Reactive] public partial bool ContainsIgnores { get; set; }
    [Reactive] public partial bool ContainsAlwaysEnableds { get; set; }

    public ObservableCollection<FileTreeItemVM> Children { get; } = new();

    /// <summary>
    ///     WPF's HasItems: a folder with anything in it gets an expander before its contents are read, which the
    ///     WPF tree did with a placeholder child.
    /// </summary>
    [Reactive] public partial bool HasItems { get; set; }

    [Reactive] public partial bool IsExpanded { get; set; }

    /// <summary>Reads the folder's contents with <paramref name="load" /> the first time it is expanded.</summary>
    public void LoadChildrenOnExpand(Func<FileTreeItemVM, IEnumerable<FileTreeItemVM>> load)
    {
        _loadChildren = load;
        this.WhenAnyValue(x => x.IsExpanded)
            .Where(expanded => expanded && _loadChildren != null)
            .Subscribe(_ =>
            {
                var loader = _loadChildren!;
                _loadChildren = null;
                foreach (var child in loader(this)) Children.Add(child);
            })
            .DisposeWith(_disposable);
    }

    private void WatchStates()
    {
        this.WhenAnyValue(x => x.CompilerFileState)
            .Subscribe(_ => UpdateCompilerFileStates())
            .DisposeWith(_disposable);

        Observable.FromEventPattern<NotifyCollectionChangedEventHandler, NotifyCollectionChangedEventArgs>(
                h => CompilerFileStates.CollectionChanged += h, h => CompilerFileStates.CollectionChanged -= h)
            .Subscribe(_ => UpdateCompilerFileStates())
            .DisposeWith(_disposable);
    }

    private void UpdateCompilerFileStates()
    {
        HasStates = CompilerFileStates.Count > 0;
        if (!CompilerFileStates.Any())
        {
            CompilerFileState = null;
            return;
        }

        // Merge the list back into the flag enum.
        CompilerFileState = CompilerFileStates.Aggregate((CompilerFileState)0, (a, b) => a | b);
    }

    public override string ToString() => Info.Name;

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _disposable.Dispose();
    }
}
