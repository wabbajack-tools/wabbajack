using System;
using System.Collections.Generic;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.DTOs;
using Wabbajack.Paths;

namespace Wabbajack.Installer.Test.Preflight.Fakes;

public sealed class FakeGameLocator : IGameLocator
{
    public Dictionary<Game, AbsolutePath> Games { get; } = new();
    public Dictionary<Game, string> SteamBuildIds { get; } = new();

    public AbsolutePath GameLocation(Game game)
    {
        if (TryFindLocation(game, out var path)) return path;
        throw new Exception($"Can't find game {game}");
    }

    public bool IsInstalled(Game game)
    {
        return Games.ContainsKey(game);
    }

    public bool TryFindLocation(Game game, out AbsolutePath path)
    {
        return Games.TryGetValue(game, out path);
    }

    public bool TryGetSteamBuildId(Game game, out string buildId)
    {
        return SteamBuildIds.TryGetValue(game, out buildId!);
    }
}
