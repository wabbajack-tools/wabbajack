using Wabbajack.DTOs;
using Wabbajack.Paths;

namespace Wabbajack.Installer
{
    public interface IGameLocator
    {
        public AbsolutePath GameLocation(Game game);
        public bool IsInstalled(Game game);
        public bool TryFindLocation(Game game, out AbsolutePath path);
    }
}