using System.Collections.Generic;
using System.Linq;
using Wabbajack.GameFinder.Common;
using Wabbajack.GameFinder.Paths;
using OneOf;

namespace Wabbajack.GameFinder.Wine.Bottles;

/// <summary>
/// Wineprefix manager for prefixes created and managed by Bottles.
/// </summary>
public class BottlesWinePrefixManager : IWinePrefixManager<BottlesWinePrefix>
{
    private readonly IFileSystem _fileSystem;

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="fs"></param>
    public BottlesWinePrefixManager(IFileSystem fs)
    {
        _fileSystem = fs;
    }

    /// <inheritdoc/>
    public IEnumerable<OneOf<BottlesWinePrefix, ErrorMessage>> FindPrefixes()
    {
        var defaultLocation = GetDefaultLocations(_fileSystem)
            .FirstOrDefault(x => _fileSystem.DirectoryExists(x));

        if (string.IsNullOrEmpty(defaultLocation.Directory))
        {
            yield return new ErrorMessage("Unable to find any bottles installation.");
            yield break;
        }

        var bottles = defaultLocation.Combine("bottles");
        if (!bottles.DirectoryExists())
        {
            yield return new ErrorMessage($"Bottles directory {bottles.GetFullPath()} does not exist");
            yield break;
        }

        foreach (var bottle in _fileSystem.EnumerateDirectories(bottles, recursive: false))
        {
            var res = IsValidBottlesPrefix(_fileSystem, bottle);
            yield return res.Match<OneOf<BottlesWinePrefix, ErrorMessage>>(
                _ => new BottlesWinePrefix
                {
                    ConfigurationDirectory = bottle,
                },
                error => error);
        }
    }

    internal static OneOf<bool, ErrorMessage> IsValidBottlesPrefix(IFileSystem fs, AbsolutePath directory)
    {
        var defaultWinePrefixRes = DefaultWinePrefixManager.IsValidPrefix(fs, directory);
        if (defaultWinePrefixRes.IsError())
        {
            return defaultWinePrefixRes.AsError();
        }

        var bottlesConfigFile = directory.Combine("bottle.yml");
        if (!fs.FileExists(bottlesConfigFile))
        {
            return new ErrorMessage($"Bottles configuration file is missing at {bottlesConfigFile}");
        }

        return true;
    }

    internal static IEnumerable<AbsolutePath> GetDefaultLocations(IFileSystem fs)
    {
        // $XDG_DATA_HOME/bottles aka ~/.local/share/bottles
        yield return fs.GetKnownPath(KnownPath.LocalApplicationDataDirectory)
            .Combine("bottles");

        // ~/.var/app/com.usebottles.bottles/data/bottles (flatpak installation)
        // https://github.com/flatpak/flatpak/wiki/Filesystem
        yield return fs.GetKnownPath(KnownPath.HomeDirectory)
            .Combine(".var/app/com.usebottles.bottles/data/bottles");
    }
}
