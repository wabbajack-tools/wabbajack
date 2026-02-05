using Wabbajack.Downloaders.GameFile;
using Wabbajack.DTOs;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.Services.OSIntegrated;

public class StubbedGameLocator : IGameLocator
{
    private readonly TemporaryPath _location;
    private readonly TemporaryFileManager _manager;

    public StubbedGameLocator(TemporaryFileManager manager)
    {
        _manager = manager;
        _location = manager.CreateFolder();
    }

    public AbsolutePath GameLocation(Game game)
    {
        return _location.Path;
    }

    public bool IsInstalled(Game game)
    {
        return true;
    }

    public bool TryFindLocation(Game game, out AbsolutePath path)
    {
        path = _location.Path;
        return true;
    }
}