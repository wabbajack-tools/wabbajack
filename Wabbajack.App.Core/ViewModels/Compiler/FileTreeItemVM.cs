using FluentIcons.Common;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using ReactiveMarbles.ObservableEvents;
using System.Reactive.Linq;
using Wabbajack.Paths;

namespace Wabbajack;

[Flags]
public enum CompilerFileState : uint
{
    [Description("No Match Include")]
    NoMatchInclude = 1,
    [Description("Include")]
    Include = 2,
    [Description("Ignore")]
    Ignore = 4,
    [Description("Always Enabled")]
    AlwaysEnabled = 8
}

/// <summary>
/// TODO: Bit of a super class for both files and folders atm, refactor?
/// </summary>
public partial class FileTreeItemVM : ReactiveObject, IDisposable
{
    private readonly CompositeDisposable _disposable = new();
    public FileSystemInfo Info { get; set; }
    public bool IsDirectory { get; set; }
    public Symbol Symbol { get; set; }
    [Reactive] public partial CompilerFileState? CompilerFileState { get; set; }
    [Reactive] public partial ObservableCollection<CompilerFileState> CompilerFileStates { get; set; } = new ObservableCollection<CompilerFileState>();

    public RelativePath PathRelativeToRoot { get; set; }
    [Reactive] public partial bool ContainsNoMatchIncludes { get; set; }
    [Reactive] public partial bool ContainsIncludes { get; set; }
    [Reactive] public partial bool ContainsIgnores { get; set; }
    [Reactive] public partial bool ContainsAlwaysEnableds { get; set; }

    /// <summary>
    /// Child nodes of this item, populated lazily for directories. Drives the view layer's
    /// HierarchicalDataTemplate / TreeDataTemplate rendering.
    /// </summary>
    public ObservableCollection<FileTreeItemVM> Children { get; } = new();

    /// <summary>
    /// Bound two-way to the tree container's expanded state; flipping true triggers lazy-load.
    /// </summary>
    [Reactive] public partial bool IsExpanded { get; set; }

    /// <summary>
    /// Lazy-load bookkeeping flag: true once <see cref="Children"/> has been realized from disk.
    /// </summary>
    public bool ChildrenLoaded { get; set; }

    /// <summary>
    /// True for the inert sentinel child added under an unrealized directory so the expander shows.
    /// The view renders these as a subtle "loading" row rather than as a real file/folder.
    /// </summary>
    public bool IsPlaceholder { get; set; }

    public FileTreeItemVM(DirectoryInfo info)
    {
        Info = info;
        IsDirectory = true;
        Symbol = Symbol.Folder;

        this.WhenAnyValue(x => x.CompilerFileState)
            .Subscribe(_ => UpdateCompilerFileStates())
            .DisposeWith(_disposable);

        this.CompilerFileStates.Events().CollectionChanged
            .Subscribe(_ => UpdateCompilerFileStates())
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
            ".js" => Symbol.DocumentJavaScript,
            ".java" => Symbol.DocumentJava,
            ".pdf" => Symbol.DocumentPdf,
            ".lua" or ".py" or ".bat" or ".reds" or ".psc" => Symbol.Receipt,
            ".exe" => Symbol.ReceiptPlay,
            ".esp" or ".esl" or ".esm" or ".archive" => Symbol.DocumentTable,
            _ => Symbol.Document
        };

        this.WhenAnyValue(x => x.CompilerFileState)
            .Subscribe(_ => UpdateCompilerFileStates())
            .DisposeWith(_disposable);

        this.CompilerFileStates.Events().CollectionChanged
            .Subscribe(s => UpdateCompilerFileStates())
            .DisposeWith(_disposable);
    }

    private void UpdateCompilerFileStates()
    {
        if (!CompilerFileStates.Any())
        {
            CompilerFileState = null;
            return;
        }

        // Merge array back into flag enum
        CompilerFileState = CompilerFileStates.Aggregate((CompilerFileState)0, (a, b) => a | b);
    }

    public override string ToString() => Info.Name;
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _disposable.Dispose();
    }
}
