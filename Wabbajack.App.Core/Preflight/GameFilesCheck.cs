using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.Preflight;

public partial class GameFilesCheck : ReactiveObject, IPreflightCheck
{
    private readonly IGameLocator _gameLocator;
    private readonly ILogger _logger;
    private readonly List<(GameFileSource State, Archive Archive, PreflightSubItem Item)> _tracked = new();

    public string Title => "Game files";
    [Reactive] public partial PreflightCheckStatus Status { get; set; }
    [Reactive] public partial string? FailureMessage { get; set; }
    [Reactive] public partial ICommand? ActionCommand { get; set; }
    [Reactive] public partial string? ActionLabel { get; set; }
    [Reactive] public partial IReadOnlyList<PreflightSubItem>? SubItems { get; set; }

    public GameFilesCheck(IReadOnlyList<Archive> allArchives, IGameLocator gameLocator, ILogger logger)
    {
        _gameLocator = gameLocator;
        _logger = logger;

        // Collect all GameFileSource archives
        foreach (var a in allArchives)
        {
            if (a.State is not GameFileSource gfs) continue;
            var item = new PreflightSubItem
            {
                Name = $"{gfs.Game.MetaData().HumanFriendlyGameName}: {gfs.GameFile}",
                SizeBytes = a.Size,
                SizeText = ""
            };
            _tracked.Add((gfs, a, item));
        }

        if (_tracked.Count == 0)
        {
            Status = PreflightCheckStatus.Passed;
            return;
        }

        _logger.LogInformation("Preflight game files: checking {Count} game files", _tracked.Count);
        CheckFiles();

        ActionCommand = ReactiveCommand.Create(CheckFiles);
        ActionLabel = "Re-check";
    }

    private void CheckFiles()
    {
        var missing = new List<PreflightSubItem>();

        foreach (var (gfs, archive, item) in _tracked)
        {
            if (!_gameLocator.TryFindLocation(gfs.Game, out var gamePath))
            {
                item.IsReady = false;
                item.StatusText = "Game not installed";
                missing.Add(item);
                continue;
            }

            var filePath = gfs.GameFile.RelativeTo(gamePath);
            if (filePath.FileExists())
            {
                item.IsReady = true;
                item.StatusText = null;
            }
            else
            {
                item.IsReady = false;
                item.StatusText = "File missing";
                missing.Add(item);
                _logger.LogWarning("Preflight: game file missing: {Game} / {File} (expected at {Path})",
                    gfs.Game, gfs.GameFile, filePath);
            }
        }

        if (missing.Count == 0)
        {
            Status = PreflightCheckStatus.Passed;
            FailureMessage = null;
            SubItems = null;
        }
        else
        {
            Status = PreflightCheckStatus.Failed;
            FailureMessage = $"{missing.Count} game files missing — verify game installation or run the game once";
            SubItems = missing;
        }
    }

    public void Dispose() { }
}
