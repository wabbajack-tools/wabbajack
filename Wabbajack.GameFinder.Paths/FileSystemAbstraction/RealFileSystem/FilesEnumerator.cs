using System;
using System.IO;
using System.IO.Enumeration;
using Wabbajack.GameFinder.Paths.Utilities;

namespace Wabbajack.GameFinder.Paths;

internal sealed class FilesEnumerator : FileSystemEnumerator<FilesEnumeratorEntry>
{
    private string? _currentDirectory;
    public string CurrentDirectory => _currentDirectory ?? _startDirectory;

    private readonly string _startDirectory;
    private readonly string _pattern;
    private readonly EnumerationOptions _options;

    public FilesEnumerator(string directory, string pattern, EnumerationOptions options) : base(directory, options)
    {
        _startDirectory = directory;
        _pattern = pattern;
        _options = options;
    }

    protected override void OnDirectoryFinished(ReadOnlySpan<char> directory)
        => _currentDirectory = null;

    protected override bool ShouldIncludeEntry(ref FileSystemEntry entry)
        => EnumeratorHelpers.MatchesPattern(_pattern, entry.FileName, _options.MatchType);

    protected override FilesEnumeratorEntry TransformEntry(ref FileSystemEntry entry)
    {
        _currentDirectory ??= PathHelpers.Sanitize(entry.Directory);
        return new FilesEnumeratorEntry(PathHelpers.Sanitize(entry.FileName), entry.IsDirectory);
    }
}

internal record struct FilesEnumeratorEntry(string FileName, bool IsDirectory);
