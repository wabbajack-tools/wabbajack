using Wabbajack.DTOs;
using Wabbajack.Paths;

namespace Wabbajack.Downloaders.GameFile;

public interface IGameLocator
{
    public AbsolutePath GameLocation(Game game);
    public bool IsInstalled(Game game);
    public bool TryFindLocation(Game game, out AbsolutePath path);
}