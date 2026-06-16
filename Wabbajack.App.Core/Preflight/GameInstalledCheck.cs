// Wabbajack.App.Wpf/Preflight/GameInstalledCheck.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.DTOs;
using Wabbajack.Paths;

namespace Wabbajack.Preflight;

public partial class GameInstalledCheck : ReactiveObject, IPreflightCheck
{
    public string Title => "Game installed";
    [Reactive] public partial PreflightCheckStatus Status { get; set; }
    [Reactive] public partial string? FailureMessage { get; set; }
    public ICommand? ActionCommand { get; }
    [Reactive] public partial string? ActionLabel { get; set; }
    public IReadOnlyList<PreflightSubItem>? SubItems => null;

    public GameInstalledCheck(IGameLocator gameLocator, Game game)
    {
        var gameName = game.MetaData().HumanFriendlyGameName;

        if (gameLocator.TryFindLocation(game, out _))
        {
            Status = PreflightCheckStatus.Passed;
            return;
        }

        // Check commonly confused games for a helpful hint
        var confused = game.MetaData().CommonlyConfusedWith
            .Where(g => gameLocator.IsInstalled(g))
            .Select(g => g.MetaData().HumanFriendlyGameName)
            .FirstOrDefault();

        Status = PreflightCheckStatus.Failed;
        FailureMessage = confused != null
            ? $"{confused} is installed, but this list requires {gameName}"
            : $"Game not found: {gameName}. Install it via Steam, GOG, or Epic Games Store.";

        ActionCommand = ReactiveCommand.Create(() =>
        {
            // Re-check game installation
            if (gameLocator.TryFindLocation(game, out _))
            {
                Status = PreflightCheckStatus.Passed;
                FailureMessage = null;
                ActionLabel = null;
            }
        });
        ActionLabel = "Re-check";
    }

    public void Dispose() { }
}
