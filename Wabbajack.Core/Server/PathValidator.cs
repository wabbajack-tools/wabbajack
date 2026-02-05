using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.DTOs;
using Wabbajack.DTOs.API;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.Server;

/// <summary>
/// Validates install and download folder paths for modlist installation.
/// </summary>
public class PathValidator
{
    private readonly GameLocator _gameLocator;

    public PathValidator(GameLocator gameLocator)
    {
        _gameLocator = gameLocator;
    }

    /// <summary>
    /// Validates the install folder path.
    /// </summary>
    public FolderValidation ValidateInstallFolder(AbsolutePath path, Game game)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        // Check if path is empty
        if (path == default || string.IsNullOrWhiteSpace(path.ToString()))
        {
            errors.Add("Install folder path is required");
            return new FolderValidation(false, errors, warnings);
        }

        // Check for invalid locations
        ValidateNotInInvalidLocation(path, errors, warnings);

        // Check if path is inside game folder
        if (_gameLocator.TryFindLocation(game, out var gamePath))
        {
            if (path == gamePath || path.InFolder(gamePath))
            {
                errors.Add("Install folder cannot be inside the game folder. This would corrupt your game installation.");
            }
        }

        // Linux-specific: Check if inside Wine/Proton prefix
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            ValidateLinuxWinePrefix(path, game, errors, warnings);
        }

        // Check if path exists and is writable
        if (path.DirectoryExists())
        {
            if (!IsDirectoryWritable(path))
            {
                errors.Add("Install folder exists but is not writable");
            }
        }
        else
        {
            // Check if parent exists and is writable
            var parent = path.Parent;
            if (!parent.DirectoryExists())
            {
                warnings.Add("Install folder and its parent directory do not exist. They will be created during installation.");
            }
            else if (!IsDirectoryWritable(parent))
            {
                errors.Add("Cannot create install folder: parent directory is not writable");
            }
        }

        return new FolderValidation(errors.Count == 0, errors, warnings);
    }

    /// <summary>
    /// Validates the download folder path.
    /// </summary>
    public FolderValidation ValidateDownloadFolder(AbsolutePath path)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        // Check if path is empty
        if (path == default || string.IsNullOrWhiteSpace(path.ToString()))
        {
            errors.Add("Download folder path is required");
            return new FolderValidation(false, errors, warnings);
        }

        // Check for invalid locations
        ValidateNotInInvalidLocation(path, errors, warnings);

        // Check if path exists and is writable
        if (path.DirectoryExists())
        {
            if (!IsDirectoryWritable(path))
            {
                errors.Add("Download folder exists but is not writable");
            }
        }
        else
        {
            // Check if parent exists and is writable
            var parent = path.Parent;
            if (!parent.DirectoryExists())
            {
                warnings.Add("Download folder and its parent directory do not exist. They will be created during installation.");
            }
            else if (!IsDirectoryWritable(parent))
            {
                errors.Add("Cannot create download folder: parent directory is not writable");
            }
        }

        return new FolderValidation(errors.Count == 0, errors, warnings);
    }

    /// <summary>
    /// Validates both install and download folders.
    /// </summary>
    public PathValidationResult ValidatePaths(string installFolder, string downloadFolder, Game game)
    {
        var installPath = installFolder.ToAbsolutePath();
        var downloadPath = downloadFolder.ToAbsolutePath();

        var installValidation = ValidateInstallFolder(installPath, game);
        var downloadValidation = ValidateDownloadFolder(downloadPath);

        return new PathValidationResult(installValidation, downloadValidation);
    }

    private void ValidateNotInInvalidLocation(AbsolutePath path, List<string> errors, List<string> warnings)
    {
        // Windows folder
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var windowsPath = KnownFolders.Windows;
            if (path.InFolder(windowsPath) || path == windowsPath)
            {
                errors.Add("Cannot use a location inside the Windows folder");
                return;
            }

            var system32Path = KnownFolders.WindowsSystem32;
            if (path.InFolder(system32Path) || path == system32Path)
            {
                errors.Add("Cannot use a location inside the System32 folder");
                return;
            }
        }

        // Check special folders
        var specialFoldersToBlock = new[]
        {
            Environment.SpecialFolder.Desktop,
            Environment.SpecialFolder.MyDocuments,
            Environment.SpecialFolder.ProgramFiles,
            Environment.SpecialFolder.ProgramFilesX86,
            Environment.SpecialFolder.CommonProgramFiles,
            Environment.SpecialFolder.CommonProgramFilesX86,
        };

        foreach (var folder in specialFoldersToBlock)
        {
            try
            {
                var folderPath = Environment.GetFolderPath(folder);
                if (string.IsNullOrEmpty(folderPath)) continue;

                var specialPath = folderPath.ToAbsolutePath();
                if (path == specialPath || path.InFolder(specialPath))
                {
                    errors.Add($"Cannot use a location inside the {folder} folder");
                    return;
                }
            }
            catch
            {
                // Some special folders may not exist on all platforms
            }
        }

        // Downloads folder check (not a SpecialFolder, need to check manually)
        var downloadsPath = GetOsDownloadsFolder();
        if (downloadsPath != default)
        {
            if (path == downloadsPath)
            {
                errors.Add("Cannot use the system Downloads folder directly. Please create a subfolder.");
            }
            else if (path.InFolder(downloadsPath))
            {
                warnings.Add("Using a location inside the system Downloads folder. Consider using a dedicated folder instead.");
            }
        }
    }

    private void ValidateLinuxWinePrefix(AbsolutePath path, Game game, List<string> errors, List<string> warnings)
    {
        // On Linux, check if the path is inside a Wine/Proton prefix
        // This is important for Windows games running under Wine/Proton

        // For now, we'll add a warning if the path doesn't look like it's in a Wine prefix
        // A proper implementation would check for .wine or Steam Proton prefixes

        var pathStr = path.ToString();

        // Check common Wine prefix patterns
        var isInWinePrefix =
            pathStr.Contains("/.wine/") ||
            pathStr.Contains("/compatdata/") ||  // Steam Proton
            pathStr.Contains("/pfx/") ||         // Steam Proton alternate
            pathStr.Contains("dosdevices/") ||
            pathStr.Contains("/drive_c/");

        if (!isInWinePrefix)
        {
            // Check if game is installed (meaning it has a Wine prefix)
            if (_gameLocator.IsInstalled(game))
            {
                var gamePath = _gameLocator.GameLocation(game).ToString();

                // If the game path is in a Wine prefix but our install path isn't, warn
                var gameInWinePrefix =
                    gamePath.Contains("/.wine/") ||
                    gamePath.Contains("/compatdata/") ||
                    gamePath.Contains("/pfx/") ||
                    gamePath.Contains("/drive_c/");

                if (gameInWinePrefix)
                {
                    warnings.Add("The game appears to be running under Wine/Proton. Consider installing the modlist inside the same Wine prefix to ensure compatibility.");
                }
            }
        }
    }

    private static bool IsDirectoryWritable(AbsolutePath path)
    {
        try
        {
            // Try to create a temp file to test write access
            var testFile = path.Combine(Path.GetRandomFileName());
            using (File.Create(testFile.ToString(), 1, FileOptions.DeleteOnClose))
            {
                // File was created successfully
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the OS Downloads folder path.
    /// </summary>
    public static AbsolutePath GetOsDownloadsFolder()
    {
        try
        {
            // On Windows, use Shell32 or fallback to UserProfile + Downloads
            // On Linux/Mac, typically ~/Downloads
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(userProfile))
            {
                return userProfile.ToAbsolutePath().Combine("Downloads");
            }
        }
        catch
        {
            // Ignore errors
        }
        return default;
    }
}
