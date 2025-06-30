using FluentIcons.Common;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using ReactiveMarbles.ObservableEvents;
using System.Reactive.Linq;
using System.Windows.Controls;
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

/// <summary>
/// TODO: Bit of a super class for both files and folders atm, refactor?
/// </summary>
public class FileTreeItemVM : ReactiveObject, IDisposable
{
    private readonly CompositeDisposable _disposable = new();
    public FileSystemInfo Info { get; set; }
    public bool IsDirectory { get; set; }
    public Symbol Symbol { get; set; }
    [Reactive] public CompilerFileState? CompilerFileState { get; set; }
    [Reactive] public ObservableCollection<CompilerFileState> CompilerFileStates { get; set; } = new ObservableCollection<CompilerFileState>();

    public RelativePath PathRelativeToRoot { get; set; }
    [Reactive] public bool ContainsNoMatchIncludes { get; set; }
    [Reactive] public bool ContainsIncludes { get; set; }
    [Reactive] public bool ContainsIgnores { get; set; }
    [Reactive] public bool ContainsAlwaysEnableds { get; set; }

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
            ".js" => Symbol.DocumentJavascript,
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
