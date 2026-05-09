// Wabbajack.App.Wpf/Preflight/PathValidationCheck.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.Preflight;

public partial class PathValidationCheck : ReactiveObject, IPreflightCheck
{
    public string Title => "Path validation";
    [Reactive] public partial PreflightCheckStatus Status { get; set; }
    [Reactive] public partial string? FailureMessage { get; set; }
    public ICommand? ActionCommand => null;
    public string? ActionLabel => null;
    public IReadOnlyList<PreflightSubItem>? SubItems => null;

    public PathValidationCheck()
    {
        Status = PreflightCheckStatus.Pending;
    }

    public void Update(AbsolutePath installPath, AbsolutePath downloadPath, IReadOnlyList<AbsolutePath> gameFolders)
    {
        var error = Validate(installPath, downloadPath, gameFolders);
        if (error == null)
        {
            Status = PreflightCheckStatus.Passed;
            FailureMessage = null;
        }
        else
        {
            Status = PreflightCheckStatus.Failed;
            FailureMessage = error;
        }
    }

    private static string? Validate(AbsolutePath installPath, AbsolutePath downloadPath, IReadOnlyList<AbsolutePath> gameFolders)
    {
        if (installPath == default || installPath.Depth <= 1)
            return "Please specify an installation location";

        if (downloadPath == default || downloadPath.Depth <= 1)
            return "Please specify a download location";

        if (installPath.InFolder(KnownFolders.Windows))
            return "Can't install to the Windows folder";

        if (installPath == downloadPath)
            return "Installation and download locations cannot be identical";

        if (KnownFolders.IsSubDirectoryOf(installPath.ToString(), downloadPath.ToString()))
            return "Can't install to a folder within the downloads folder";

        foreach (var gameFolder in gameFolders)
        {
            if (installPath.InFolder(gameFolder))
                return "Can't install into a game folder";

            if (gameFolder.ThisAndAllParents().Any(path => installPath == path))
                return "Can't install to path — installed files may overwrite game files";
        }

        if (installPath.InFolder(KnownFolders.EntryPoint))
            return "Can't install into the Wabbajack folder";
        if (downloadPath.InFolder(KnownFolders.EntryPoint))
            return "Can't download into the Wabbajack folder";
        if (KnownFolders.EntryPoint.ThisAndAllParents().Any(path => installPath == path))
            return "Can't install into the Wabbajack folder";

        if (KnownFolders.IsInSpecialFolder(installPath, out var specialFolder))
            return $"Can't install into special folder ({specialFolder})";
        if (KnownFolders.IsInSpecialFolder(downloadPath, out var specialDownloadsFolder))
            return $"Can't download into special folder ({specialDownloadsFolder})";

        return null;
    }

    public void Dispose() { }
}
