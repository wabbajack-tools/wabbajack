# Preflight Check Page Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the install configuration view with a reactive preflight check page that validates game installation, disk space, Nexus login, downloads, and path validity before enabling installation.

**Architecture:** A `PreflightViewModel` owns a list of `IPreflightCheck` implementations. Each check is self-contained with reactive status. The VM computes a summary (passed/total) and gates the Install button. The view is a single `PreflightView.xaml` using `ItemsControl` with `DataTemplate` for check cards.

**Tech Stack:** WPF, ReactiveUI (20.x with SourceGenerators), MahApps.Metro, `FileSystemWatcher`, `System.IO.Hashing.XxHash64`

**Spec:** `docs/superpowers/specs/2026-05-09-preflight-check-page-design.md`

---

### Task 1: IPreflightCheck Interface and Types

**Files:**
- Create: `Wabbajack.App.Wpf/Preflight/IPreflightCheck.cs`
- Create: `Wabbajack.App.Wpf/Preflight/PreflightSubItem.cs`

- [ ] **Step 1: Create the `IPreflightCheck` interface and enum**

```csharp
// Wabbajack.App.Wpf/Preflight/IPreflightCheck.cs
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Input;

namespace Wabbajack.Preflight;

public enum PreflightCheckStatus { Pending, Checking, Passed, Failed }

public interface IPreflightCheck : INotifyPropertyChanged, IDisposable
{
    string Title { get; }
    PreflightCheckStatus Status { get; }
    string? FailureMessage { get; }
    ICommand? ActionCommand { get; }
    string? ActionLabel { get; }
    IReadOnlyList<PreflightSubItem>? SubItems { get; }
}
```

- [ ] **Step 2: Create `PreflightSubItem`**

```csharp
// Wabbajack.App.Wpf/Preflight/PreflightSubItem.cs
using System.Windows.Input;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace Wabbajack.Preflight;

public partial class PreflightSubItem : ReactiveObject
{
    public required string Name { get; init; }
    public required string SizeText { get; init; }
    [Reactive] public partial string? StatusText { get; set; }
    [Reactive] public partial double? Progress { get; set; }
    public ICommand? ActionCommand { get; init; }
    public string? ActionLabel { get; init; }
}
```

- [ ] **Step 3: Commit**

```bash
git add Wabbajack.App.Wpf/Preflight/IPreflightCheck.cs Wabbajack.App.Wpf/Preflight/PreflightSubItem.cs
git commit -m "feat: add IPreflightCheck interface and PreflightSubItem types"
```

---

### Task 2: PathValidationCheck

**Files:**
- Create: `Wabbajack.App.Wpf/Preflight/PathValidationCheck.cs`
- Create: `Wabbajack.Test/Preflight/PathValidationCheckTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// Wabbajack.Test/Preflight/PathValidationCheckTests.cs
using Wabbajack.Preflight;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Xunit;

namespace Wabbajack.Preflight.Test;

public class PathValidationCheckTests
{
    [Fact]
    public void ValidPaths_Pass()
    {
        var check = new PathValidationCheck();
        check.Update(
            (AbsolutePath)@"D:\Modlists\TestList",
            (AbsolutePath)@"D:\Modlists\TestList\downloads",
            Array.Empty<AbsolutePath>());

        Assert.Equal(PreflightCheckStatus.Passed, check.Status);
    }

    [Fact]
    public void EmptyInstallPath_Fails()
    {
        var check = new PathValidationCheck();
        check.Update(
            default,
            (AbsolutePath)@"D:\Downloads",
            Array.Empty<AbsolutePath>());

        Assert.Equal(PreflightCheckStatus.Failed, check.Status);
        Assert.Contains("installation location", check.FailureMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void IdenticalPaths_Fails()
    {
        var check = new PathValidationCheck();
        check.Update(
            (AbsolutePath)@"D:\Modlists\TestList",
            (AbsolutePath)@"D:\Modlists\TestList",
            Array.Empty<AbsolutePath>());

        Assert.Equal(PreflightCheckStatus.Failed, check.Status);
        Assert.Contains("identical", check.FailureMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InstallInGameFolder_Fails()
    {
        var gameFolders = new[] { (AbsolutePath)@"C:\Games\Skyrim" };
        var check = new PathValidationCheck();
        check.Update(
            (AbsolutePath)@"C:\Games\Skyrim\mods",
            (AbsolutePath)@"D:\Downloads",
            gameFolders);

        Assert.Equal(PreflightCheckStatus.Failed, check.Status);
        Assert.Contains("game folder", check.FailureMessage, StringComparison.OrdinalIgnoreCase);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Wabbajack.Test --filter "FullyQualifiedName~PathValidationCheckTests" -v n`
Expected: FAIL — `PathValidationCheck` does not exist yet.

- [ ] **Step 3: Implement `PathValidationCheck`**

```csharp
// Wabbajack.App.Wpf/Preflight/PathValidationCheck.cs
using System;
using System.Collections.Generic;
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
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test Wabbajack.Test --filter "FullyQualifiedName~PathValidationCheckTests" -v n`
Expected: All 4 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add Wabbajack.App.Wpf/Preflight/PathValidationCheck.cs Wabbajack.Test/Preflight/PathValidationCheckTests.cs
git commit -m "feat: add PathValidationCheck with tests"
```

---

### Task 3: GameInstalledCheck

**Files:**
- Create: `Wabbajack.App.Wpf/Preflight/GameInstalledCheck.cs`
- Create: `Wabbajack.Test/Preflight/GameInstalledCheckTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// Wabbajack.Test/Preflight/GameInstalledCheckTests.cs
using NSubstitute;
using Wabbajack.DTOs;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.Paths;
using Wabbajack.Preflight;
using Xunit;

namespace Wabbajack.Preflight.Test;

public class GameInstalledCheckTests
{
    [Fact]
    public void GameInstalled_Passes()
    {
        var locator = Substitute.For<IGameLocator>();
        AbsolutePath path = (AbsolutePath)@"C:\Games\Skyrim";
        locator.TryFindLocation(Game.SkyrimSpecialEdition, out Arg.Any<AbsolutePath>())
            .Returns(x => { x[1] = path; return true; });

        var check = new GameInstalledCheck(locator, Game.SkyrimSpecialEdition);

        Assert.Equal(PreflightCheckStatus.Passed, check.Status);
    }

    [Fact]
    public void GameNotInstalled_Fails()
    {
        var locator = Substitute.For<IGameLocator>();
        locator.TryFindLocation(Game.SkyrimSpecialEdition, out Arg.Any<AbsolutePath>())
            .Returns(false);

        var check = new GameInstalledCheck(locator, Game.SkyrimSpecialEdition);

        Assert.Equal(PreflightCheckStatus.Failed, check.Status);
        Assert.Contains("Skyrim", check.FailureMessage);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Wabbajack.Test --filter "FullyQualifiedName~GameInstalledCheckTests" -v n`
Expected: FAIL — `GameInstalledCheck` does not exist yet.

- [ ] **Step 3: Implement `GameInstalledCheck`**

```csharp
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
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test Wabbajack.Test --filter "FullyQualifiedName~GameInstalledCheckTests" -v n`
Expected: All 2 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add Wabbajack.App.Wpf/Preflight/GameInstalledCheck.cs Wabbajack.Test/Preflight/GameInstalledCheckTests.cs
git commit -m "feat: add GameInstalledCheck with tests"
```

---

### Task 4: DiskSpaceCheck

**Files:**
- Create: `Wabbajack.App.Wpf/Preflight/DiskSpaceCheck.cs`
- Create: `Wabbajack.Test/Preflight/DiskSpaceCheckTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// Wabbajack.Test/Preflight/DiskSpaceCheckTests.cs
using Wabbajack.Paths;
using Wabbajack.Preflight;
using Xunit;

namespace Wabbajack.Preflight.Test;

public class DiskSpaceCheckTests
{
    [Fact]
    public void SufficientSpace_Passes()
    {
        var check = new DiskSpaceCheck();
        // Use current drive which should have some free space
        var testPath = (AbsolutePath)AppDomain.CurrentDomain.BaseDirectory;
        var driveInfo = new DriveInfo(testPath.ToString()[..1]);
        var smallSize = 1024L; // 1 KB — should always fit

        check.Update(testPath, testPath, smallSize, smallSize, 0);

        Assert.Equal(PreflightCheckStatus.Passed, check.Status);
    }

    [Fact]
    public void InsufficientInstallSpace_Fails()
    {
        var check = new DiskSpaceCheck();
        var testPath = (AbsolutePath)AppDomain.CurrentDomain.BaseDirectory;
        var hugeSize = long.MaxValue / 2;

        check.Update(testPath, testPath, hugeSize, 0, 0);

        Assert.Equal(PreflightCheckStatus.Failed, check.Status);
        Assert.Contains("disk space", check.FailureMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AlreadyDownloadedArchives_ReduceRequired()
    {
        var check = new DiskSpaceCheck();
        var testPath = (AbsolutePath)AppDomain.CurrentDomain.BaseDirectory;
        var driveInfo = new DriveInfo(testPath.ToString()[..1]);
        var freeSpace = driveInfo.AvailableFreeSpace;

        // Total archives huge, but already-present covers most of it
        var totalArchiveSize = freeSpace + 1000;
        var alreadyPresent = totalArchiveSize - 100; // only 100 bytes still needed

        check.Update(testPath, testPath, 0, totalArchiveSize, alreadyPresent);

        Assert.Equal(PreflightCheckStatus.Passed, check.Status);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Wabbajack.Test --filter "FullyQualifiedName~DiskSpaceCheckTests" -v n`
Expected: FAIL — `DiskSpaceCheck` does not exist yet.

- [ ] **Step 3: Implement `DiskSpaceCheck`**

```csharp
// Wabbajack.App.Wpf/Preflight/DiskSpaceCheck.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Input;
using Humanizer;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Wabbajack.Paths;

namespace Wabbajack.Preflight;

public partial class DiskSpaceCheck : ReactiveObject, IPreflightCheck
{
    public string Title => "Disk space";
    [Reactive] public partial PreflightCheckStatus Status { get; set; }
    [Reactive] public partial string? FailureMessage { get; set; }
    public ICommand? ActionCommand => null;
    public string? ActionLabel => null;
    public IReadOnlyList<PreflightSubItem>? SubItems => null;

    public DiskSpaceCheck()
    {
        Status = PreflightCheckStatus.Pending;
    }

    /// <summary>
    /// Re-evaluate disk space requirements.
    /// </summary>
    /// <param name="installPath">Target install directory</param>
    /// <param name="downloadPath">Target download directory</param>
    /// <param name="requiredInstallSize">Bytes needed for installed files</param>
    /// <param name="totalArchiveSize">Total bytes of all archives in the modlist</param>
    /// <param name="presentArchiveSize">Bytes of archives already present in the download folder</param>
    public void Update(AbsolutePath installPath, AbsolutePath downloadPath,
        long requiredInstallSize, long totalArchiveSize, long presentArchiveSize)
    {
        var remainingDownloadSize = Math.Max(0, totalArchiveSize - presentArchiveSize);

        try
        {
            var installDrive = new DriveInfo(installPath.ToString()[..1]);
            var downloadDrive = new DriveInfo(downloadPath.ToString()[..1]);
            var sameDrive = installDrive.Name == downloadDrive.Name;

            if (sameDrive)
            {
                var totalNeeded = requiredInstallSize + remainingDownloadSize;
                if (totalNeeded > installDrive.AvailableFreeSpace)
                {
                    Status = PreflightCheckStatus.Failed;
                    FailureMessage = $"Not enough disk space — need {totalNeeded.Bytes()}, only {installDrive.AvailableFreeSpace.Bytes()} free on {installDrive.Name}";
                    return;
                }
            }
            else
            {
                if (requiredInstallSize > installDrive.AvailableFreeSpace)
                {
                    Status = PreflightCheckStatus.Failed;
                    FailureMessage = $"Not enough disk space — install needs {requiredInstallSize.Bytes()}, only {installDrive.AvailableFreeSpace.Bytes()} free on {installDrive.Name}";
                    return;
                }

                if (remainingDownloadSize > downloadDrive.AvailableFreeSpace)
                {
                    Status = PreflightCheckStatus.Failed;
                    FailureMessage = $"Not enough disk space — downloads need {remainingDownloadSize.Bytes()}, only {downloadDrive.AvailableFreeSpace.Bytes()} free on {downloadDrive.Name}";
                    return;
                }
            }

            Status = PreflightCheckStatus.Passed;
            FailureMessage = null;
        }
        catch (Exception ex)
        {
            Status = PreflightCheckStatus.Failed;
            FailureMessage = $"Could not check disk space: {ex.Message}";
        }
    }

    public void Dispose() { }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test Wabbajack.Test --filter "FullyQualifiedName~DiskSpaceCheckTests" -v n`
Expected: All 3 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add Wabbajack.App.Wpf/Preflight/DiskSpaceCheck.cs Wabbajack.Test/Preflight/DiskSpaceCheckTests.cs
git commit -m "feat: add DiskSpaceCheck with tests"
```

---

### Task 5: NexusLoginCheck

**Files:**
- Create: `Wabbajack.App.Wpf/Preflight/NexusLoginCheck.cs`
- Create: `Wabbajack.Test/Preflight/NexusLoginCheckTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// Wabbajack.Test/Preflight/NexusLoginCheckTests.cs
using System.Linq;
using NSubstitute;
using Wabbajack.LoginManagers;
using Wabbajack.Preflight;
using Xunit;

namespace Wabbajack.Preflight.Test;

public class NexusLoginCheckTests
{
    private INeedsLogin CreateMockNexusLogin(bool loggedIn)
    {
        var mock = Substitute.For<INeedsLogin>();
        mock.SiteName.Returns("Nexus Mods");
        mock.LoggedIn.Returns(loggedIn);
        mock.LoginFor().Returns(typeof(Wabbajack.Downloaders.NexusDownloader));
        return mock;
    }

    [Fact]
    public void LoggedIn_Passes()
    {
        var login = CreateMockNexusLogin(true);
        var check = new NexusLoginCheck(login);

        Assert.Equal(PreflightCheckStatus.Passed, check.Status);
    }

    [Fact]
    public void NotLoggedIn_Fails()
    {
        var login = CreateMockNexusLogin(false);
        var check = new NexusLoginCheck(login);

        Assert.Equal(PreflightCheckStatus.Failed, check.Status);
        Assert.Contains("Nexus", check.FailureMessage);
        Assert.NotNull(check.ActionCommand);
        Assert.Equal("Log In", check.ActionLabel);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Wabbajack.Test --filter "FullyQualifiedName~NexusLoginCheckTests" -v n`
Expected: FAIL — `NexusLoginCheck` does not exist yet.

- [ ] **Step 3: Implement `NexusLoginCheck`**

The check wraps an `INeedsLogin` instance (the `NexusLoginManager`). It observes the `LoggedIn` property reactively. The `ActionCommand` delegates to the login manager's `TriggerLogin`.

```csharp
// Wabbajack.App.Wpf/Preflight/NexusLoginCheck.cs
using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows.Input;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Wabbajack.LoginManagers;

namespace Wabbajack.Preflight;

public partial class NexusLoginCheck : ReactiveObject, IPreflightCheck
{
    private readonly CompositeDisposable _disposable = new();

    public string Title => "Nexus Mods login";
    [Reactive] public partial PreflightCheckStatus Status { get; set; }
    [Reactive] public partial string? FailureMessage { get; set; }
    public ICommand? ActionCommand { get; }
    public string? ActionLabel => Status == PreflightCheckStatus.Failed ? "Log In" : null;
    public IReadOnlyList<PreflightSubItem>? SubItems => null;

    public NexusLoginCheck(INeedsLogin nexusLogin)
    {
        ActionCommand = nexusLogin.TriggerLogin;

        // Evaluate immediately
        UpdateStatus(nexusLogin.LoggedIn);

        // Observe changes reactively
        nexusLogin.WhenAnyValue(x => x.LoggedIn)
            .Subscribe(loggedIn => UpdateStatus(loggedIn))
            .DisposeWith(_disposable);
    }

    private void UpdateStatus(bool loggedIn)
    {
        if (loggedIn)
        {
            Status = PreflightCheckStatus.Passed;
            FailureMessage = null;
        }
        else
        {
            Status = PreflightCheckStatus.Failed;
            FailureMessage = "Log in to Nexus Mods to download mods automatically";
        }
        this.RaisePropertyChanged(nameof(ActionLabel));
    }

    public void Dispose() => _disposable.Dispose();
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test Wabbajack.Test --filter "FullyQualifiedName~NexusLoginCheckTests" -v n`
Expected: All 2 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add Wabbajack.App.Wpf/Preflight/NexusLoginCheck.cs Wabbajack.Test/Preflight/NexusLoginCheckTests.cs
git commit -m "feat: add NexusLoginCheck with tests"
```

---

### Task 6: DownloadsCheck

**Files:**
- Create: `Wabbajack.App.Wpf/Preflight/DownloadsCheck.cs`
- Create: `Wabbajack.Test/Preflight/DownloadsCheckTests.cs`

This is the most complex check. It classifies archives, watches folders, hashes candidates, and moves matched files.

- [ ] **Step 1: Write failing tests**

```csharp
// Wabbajack.Test/Preflight/DownloadsCheckTests.cs
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.Preflight;
using Xunit;

namespace Wabbajack.Preflight.Test;

public class DownloadsCheckTests : IDisposable
{
    private readonly TemporaryFileManager _tempManager;
    private readonly AbsolutePath _downloadDir;
    private readonly AbsolutePath _watchDir;

    public DownloadsCheckTests()
    {
        _tempManager = new TemporaryFileManager();
        _downloadDir = _tempManager.CreateFolder().Path;
        _watchDir = _tempManager.CreateFolder().Path;
    }

    private Archive MakeArchive(string name, long size, Hash hash, IDownloadState state)
    {
        return new Archive { Name = name, Size = size, Hash = hash, State = state };
    }

    [Fact]
    public void NoManualArchives_Passes()
    {
        // All archives are Nexus (premium) or HTTP — nothing manual
        var archives = new[]
        {
            MakeArchive("mod1.zip", 100, new Hash(1), new Nexus { Game = Game.SkyrimSpecialEdition, ModID = 1, FileID = 1 }),
            MakeArchive("mod2.zip", 200, new Hash(2), new Http { Url = new Uri("https://example.com/mod2.zip") }),
        };

        var check = new DownloadsCheck(archives, _downloadDir, _watchDir, isPremium: true);

        Assert.Equal(PreflightCheckStatus.Passed, check.Status);
    }

    [Fact]
    public void ManualArchivesMissing_Fails()
    {
        var archives = new[]
        {
            MakeArchive("manual.zip", 100, new Hash(1), new Manual { Url = new Uri("https://example.com/manual") }),
        };

        var check = new DownloadsCheck(archives, _downloadDir, _watchDir, isPremium: true);

        Assert.Equal(PreflightCheckStatus.Failed, check.Status);
        Assert.Equal(1, check.SubItems!.Count);
        Assert.Equal("manual.zip", check.SubItems[0].Name);
    }

    [Fact]
    public void NonPremium_NexusArchivesAreManual()
    {
        var archives = new[]
        {
            MakeArchive("nexusmod.zip", 100, new Hash(1), new Nexus { Game = Game.SkyrimSpecialEdition, ModID = 1, FileID = 1 }),
        };

        var check = new DownloadsCheck(archives, _downloadDir, _watchDir, isPremium: false);

        Assert.Equal(PreflightCheckStatus.Failed, check.Status);
        Assert.Equal(1, check.SubItems!.Count);
    }

    [Fact]
    public async Task ArchiveAlreadyInDownloadFolder_Passes()
    {
        // Write a file to the download dir that matches
        var data = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        var hash = await data.Hash();
        var filePath = _downloadDir.Combine("existing.zip");
        await filePath.WriteAllBytesAsync(data);

        var archives = new[]
        {
            MakeArchive("existing.zip", data.Length, hash, new Manual { Url = new Uri("https://example.com") }),
        };

        var check = new DownloadsCheck(archives, _downloadDir, _watchDir, isPremium: true);
        await check.ScanExistingFiles(CancellationToken.None);

        Assert.Equal(PreflightCheckStatus.Passed, check.Status);
    }

    public void Dispose()
    {
        _tempManager.Dispose();
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Wabbajack.Test --filter "FullyQualifiedName~DownloadsCheckTests" -v n`
Expected: FAIL — `DownloadsCheck` does not exist yet.

- [ ] **Step 3: Implement `DownloadsCheck`**

```csharp
// Wabbajack.App.Wpf/Preflight/DownloadsCheck.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Hashing;
using System.Linq;
using System.Reactive.Disposables;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Humanizer;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.Preflight;

public partial class DownloadsCheck : ReactiveObject, IPreflightCheck
{
    private readonly CompositeDisposable _disposable = new();
    private readonly AbsolutePath _downloadDir;
    private readonly Dictionary<Hash, Archive> _missingManual;
    private readonly HashSet<Hash> _presentHashes = new();
    private readonly object _lock = new();

    public string Title => "Downloads";
    [Reactive] public partial PreflightCheckStatus Status { get; set; }
    [Reactive] public partial string? FailureMessage { get; set; }
    public ICommand? ActionCommand => null;
    public string? ActionLabel => null;
    [Reactive] public partial IReadOnlyList<PreflightSubItem>? SubItems { get; set; }

    /// <summary>Total bytes of archives confirmed present in the download folder.</summary>
    public long PresentArchiveSize { get; private set; }

    public DownloadsCheck(IReadOnlyList<Archive> allArchives, AbsolutePath downloadDir,
        AbsolutePath systemDownloadsDir, bool isPremium)
    {
        _downloadDir = downloadDir;

        // Classify which archives need manual download
        _missingManual = allArchives
            .Where(a => IsManualDownload(a, isPremium))
            .ToDictionary(a => a.Hash, a => a);

        UpdateStatus();
        StartWatching(downloadDir, systemDownloadsDir);
    }

    private static bool IsManualDownload(Archive archive, bool isPremium)
    {
        return archive.State switch
        {
            Http => false,        // always auto-downloadable
            GameFileSource => false, // sourced from game files
            Nexus => !isPremium,  // auto only for premium
            _ => true,            // everything else is manual
        };
    }

    /// <summary>
    /// Scan files already present in the download directory to find archives that are already downloaded.
    /// Call this after construction to check existing files.
    /// </summary>
    public async Task ScanExistingFiles(CancellationToken token)
    {
        if (!_downloadDir.DirectoryExists()) return;

        foreach (var file in _downloadDir.EnumerateFiles())
        {
            await TryMatchFile(file, token);
        }
    }

    private async Task TryMatchFile(AbsolutePath filePath, CancellationToken token)
    {
        if (!filePath.FileExists()) return;

        var fileSize = filePath.Size();

        // Find archives matching this file's size
        List<Archive> candidates;
        lock (_lock)
        {
            candidates = _missingManual.Values.Where(a => a.Size == fileSize).ToList();
        }

        if (candidates.Count == 0) return;

        // Find the matching sub-item and show progress
        PreflightSubItem? subItem = null;
        var fileName = filePath.FileName.ToString();
        lock (_lock)
        {
            subItem = SubItems?.FirstOrDefault(s => candidates.Any(c => c.Name == s.Name));
        }

        if (subItem != null)
        {
            subItem.StatusText = $"Verifying {fileName}...";
            subItem.Progress = 0;
        }

        // Hash the file
        Hash hash;
        try
        {
            await using var stream = filePath.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
            var hasher = new XxHash64();
            var buffer = new byte[1024 * 1024];
            long totalRead = 0;
            while (true)
            {
                var read = await stream.ReadAsync(buffer, token);
                if (read == 0) break;
                hasher.Append(buffer.AsSpan(0, read));
                totalRead += read;
                if (subItem != null && fileSize > 0)
                    subItem.Progress = (double)totalRead / fileSize;
            }
            hash = new Hash(hasher.GetCurrentHashAsUInt64());
        }
        catch
        {
            if (subItem != null)
            {
                subItem.StatusText = null;
                subItem.Progress = null;
            }
            return;
        }

        if (subItem != null)
        {
            subItem.StatusText = null;
            subItem.Progress = null;
        }

        // Check if the hash matches any missing archive
        lock (_lock)
        {
            if (!_missingManual.ContainsKey(hash)) return;
            var archive = _missingManual[hash];

            // Move file to download dir if it's not already there
            var destPath = _downloadDir.Combine(archive.Name);
            if (filePath != destPath)
            {
                try
                {
                    if (destPath.FileExists()) destPath.Delete();
                    File.Move(filePath.ToString(), destPath.ToString());
                }
                catch
                {
                    // If move fails (e.g., cross-drive), try copy
                    try
                    {
                        File.Copy(filePath.ToString(), destPath.ToString(), true);
                    }
                    catch { return; }
                }
            }

            _missingManual.Remove(hash);
            _presentHashes.Add(hash);
            PresentArchiveSize += archive.Size;
        }

        UpdateStatus();
    }

    private void StartWatching(AbsolutePath downloadDir, AbsolutePath systemDownloadsDir)
    {
        WatchDirectory(downloadDir);
        if (systemDownloadsDir != downloadDir && systemDownloadsDir != default)
            WatchDirectory(systemDownloadsDir);
    }

    private void WatchDirectory(AbsolutePath dir)
    {
        if (!dir.DirectoryExists()) return;

        var watcher = new FileSystemWatcher(dir.ToString())
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.LastWrite,
            EnableRaisingEvents = true,
            IncludeSubdirectories = false
        };

        watcher.Created += (_, e) => OnFileChanged(e.FullPath);
        watcher.Changed += (_, e) => OnFileChanged(e.FullPath);
        watcher.Renamed += (_, e) => OnFileChanged(e.FullPath);

        _disposable.Add(watcher);
    }

    private void OnFileChanged(string fullPath)
    {
        // Debounce: slight delay to let file finish writing
        Task.Run(async () =>
        {
            await Task.Delay(500);
            await TryMatchFile((AbsolutePath)fullPath, CancellationToken.None);
        });
    }

    private void UpdateStatus()
    {
        lock (_lock)
        {
            if (_missingManual.Count == 0)
            {
                Status = PreflightCheckStatus.Passed;
                FailureMessage = null;
                SubItems = null;
            }
            else
            {
                Status = PreflightCheckStatus.Failed;
                FailureMessage = $"Download these files — they'll be detected automatically";
                SubItems = _missingManual.Values.Select(a => new PreflightSubItem
                {
                    Name = a.Name,
                    SizeText = a.Size.Bytes().ToString(),
                    ActionCommand = ReactiveCommand.Create(() =>
                    {
                        var url = GetDownloadUrl(a);
                        if (!string.IsNullOrEmpty(url))
                            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                    }),
                    ActionLabel = "Download"
                }).ToList();
            }

            this.RaisePropertyChanged(nameof(Title));
        }
    }

    private static string GetDownloadUrl(Archive archive)
    {
        return archive.State switch
        {
            Manual manual => manual.Url.ToString(),
            MediaFire mediaFire => mediaFire.Url.ToString(),
            Mega mega => mega.Url.ToString(),
            IPS4OAuth2 ips4 => ips4.LinkUrl.ToString(),
            GoogleDrive gd => gd.GetUri().ToString(),
            Http http => http.Url.ToString(),
            Nexus nexus => $"{nexus.LinkUrl}?tab=files&file_id={nexus.FileID}",
            _ => string.Empty
        };
    }

    public void Dispose() => _disposable.Dispose();
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test Wabbajack.Test --filter "FullyQualifiedName~DownloadsCheckTests" -v n`
Expected: All 4 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add Wabbajack.App.Wpf/Preflight/DownloadsCheck.cs Wabbajack.Test/Preflight/DownloadsCheckTests.cs
git commit -m "feat: add DownloadsCheck with file watching and tests"
```

---

### Task 7: PreflightViewModel

**Files:**
- Create: `Wabbajack.App.Wpf/Preflight/PreflightViewModel.cs`
- Create: `Wabbajack.Test/Preflight/PreflightViewModelTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// Wabbajack.Test/Preflight/PreflightViewModelTests.cs
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Input;
using NSubstitute;
using ReactiveUI;
using Wabbajack.Preflight;
using Xunit;

namespace Wabbajack.Preflight.Test;

public class PreflightViewModelTests
{
    private class FakeCheck : ReactiveObject, IPreflightCheck
    {
        private PreflightCheckStatus _status;
        public string Title { get; init; } = "Fake";
        public PreflightCheckStatus Status
        {
            get => _status;
            set => this.RaiseAndSetIfChanged(ref _status, value);
        }
        public string? FailureMessage { get; set; }
        public ICommand? ActionCommand => null;
        public string? ActionLabel => null;
        public IReadOnlyList<PreflightSubItem>? SubItems => null;
        public void Dispose() { }
    }

    [Fact]
    public void AllChecksPassed_InstallEnabled()
    {
        var checks = new IPreflightCheck[]
        {
            new FakeCheck { Status = PreflightCheckStatus.Passed },
            new FakeCheck { Status = PreflightCheckStatus.Passed },
        };

        var vm = new PreflightViewModel(checks);

        Assert.True(vm.AllPassed);
        Assert.Equal(2, vm.PassedCount);
        Assert.Equal(2, vm.TotalCount);
        Assert.Empty(vm.FailedChecks);
    }

    [Fact]
    public void SomeChecksFailed_InstallDisabled()
    {
        var checks = new IPreflightCheck[]
        {
            new FakeCheck { Status = PreflightCheckStatus.Passed },
            new FakeCheck { Status = PreflightCheckStatus.Failed, FailureMessage = "Oops" },
        };

        var vm = new PreflightViewModel(checks);

        Assert.False(vm.AllPassed);
        Assert.Equal(1, vm.PassedCount);
        Assert.Single(vm.FailedChecks);
    }

    [Fact]
    public void CheckTransitionsToPass_UpdatesSummary()
    {
        var failingCheck = new FakeCheck { Status = PreflightCheckStatus.Failed, FailureMessage = "Oops" };
        var checks = new IPreflightCheck[]
        {
            new FakeCheck { Status = PreflightCheckStatus.Passed },
            failingCheck,
        };

        var vm = new PreflightViewModel(checks);
        Assert.False(vm.AllPassed);

        // Transition to passed
        failingCheck.Status = PreflightCheckStatus.Passed;

        Assert.True(vm.AllPassed);
        Assert.Equal(2, vm.PassedCount);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Wabbajack.Test --filter "FullyQualifiedName~PreflightViewModelTests" -v n`
Expected: FAIL — `PreflightViewModel` does not exist yet.

- [ ] **Step 3: Implement `PreflightViewModel`**

```csharp
// Wabbajack.App.Wpf/Preflight/PreflightViewModel.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows.Input;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Wabbajack.DTOs;

namespace Wabbajack.Preflight;

public partial class PreflightViewModel : ReactiveObject, IDisposable
{
    private readonly CompositeDisposable _disposable = new();
    private readonly IPreflightCheck[] _checks;

    // Modlist info
    public string ModlistName { get; init; } = "";
    public string ModlistVersion { get; init; } = "";
    public string ModlistAuthor { get; init; } = "";
    public string? ReadmeUrl { get; init; }

    // Summary
    [Reactive] public partial int PassedCount { get; set; }
    [Reactive] public partial int TotalCount { get; set; }
    [Reactive] public partial bool AllPassed { get; set; }
    [Reactive] public partial IReadOnlyList<IPreflightCheck> FailedChecks { get; set; }

    // Commands
    public ICommand? ViewReadmeCommand { get; }
    [Reactive] public partial ICommand? InstallCommand { get; set; }

    public PreflightViewModel(IReadOnlyList<IPreflightCheck> checks)
    {
        _checks = checks.ToArray();
        TotalCount = _checks.Length;
        FailedChecks = Array.Empty<IPreflightCheck>();

        // Set up readme command
        ViewReadmeCommand = ReactiveCommand.Create(
            () => Process.Start(new ProcessStartInfo(ReadmeUrl!) { UseShellExecute = true }),
            this.WhenAnyValue(x => x.ReadmeUrl).Select(url => !string.IsNullOrWhiteSpace(url)));

        // Observe every check's Status property
        var statusObservables = _checks.Select(check =>
            check.WhenAnyValue(c => c.Status).Select(_ => System.Reactive.Unit.Default));

        Observable.Merge(statusObservables)
            .StartWith(System.Reactive.Unit.Default)
            .Subscribe(_ => Recompute())
            .DisposeWith(_disposable);
    }

    private void Recompute()
    {
        PassedCount = _checks.Count(c => c.Status == PreflightCheckStatus.Passed);
        AllPassed = PassedCount == TotalCount;
        FailedChecks = _checks.Where(c => c.Status != PreflightCheckStatus.Passed).ToList();
    }

    public void Dispose()
    {
        _disposable.Dispose();
        foreach (var check in _checks)
            check.Dispose();
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test Wabbajack.Test --filter "FullyQualifiedName~PreflightViewModelTests" -v n`
Expected: All 3 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add Wabbajack.App.Wpf/Preflight/PreflightViewModel.cs Wabbajack.Test/Preflight/PreflightViewModelTests.cs
git commit -m "feat: add PreflightViewModel with reactive summary and tests"
```

---

### Task 8: PreflightView XAML

**Files:**
- Create: `Wabbajack.App.Wpf/Views/Preflight/PreflightView.xaml`
- Create: `Wabbajack.App.Wpf/Views/Preflight/PreflightView.xaml.cs`

- [ ] **Step 1: Create the view XAML**

This uses `ReactiveUserControl<PreflightViewModel>`, MahApps.Metro styling, and `ItemsControl` for the failed checks list. Refer to `Wabbajack.App.Wpf/Views/Installers/InstallationView.xaml` for existing style conventions (e.g., `StaticResource TitleStyle`, `StaticResource Panel`, `StaticResource PickerStyle`). Use the existing `FilePicker` control for path pickers. Use `FluentIcons.Wpf:SymbolIcon` for icons (see existing usage in `Wabbajack.App.Wpf/Views/`).

```xml
<!-- Wabbajack.App.Wpf/Views/Preflight/PreflightView.xaml -->
<rxui:ReactiveUserControl
    x:Class="Wabbajack.Views.Preflight.PreflightView"
    x:TypeArguments="preflight:PreflightViewModel"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:rxui="http://reactiveui.net"
    xmlns:preflight="clr-namespace:Wabbajack.Preflight"
    xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
    xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
    mc:Ignorable="d"
    d:DataContext="{d:DesignInstance preflight:PreflightViewModel}">

    <ScrollViewer VerticalScrollBarVisibility="Auto" Padding="24">
        <StackPanel>
            <!-- Header: Modlist name + View Readme -->
            <DockPanel Margin="0,0,0,16">
                <Button x:Name="ViewReadmeButton" DockPanel.Dock="Right"
                        Content="View Readme" Padding="12,6" />
                <StackPanel>
                    <TextBlock x:Name="ModlistTitle" FontSize="20" FontWeight="Bold"
                               Foreground="White" />
                    <TextBlock x:Name="ModlistMeta" FontSize="12"
                               Foreground="#888" Margin="0,4,0,0" />
                </StackPanel>
            </DockPanel>

            <Separator Margin="0,0,0,16" Background="#333" />

            <!-- Path pickers -->
            <StackPanel Margin="0,0,0,8">
                <TextBlock Text="Install Location" FontWeight="SemiBold"
                           Foreground="#aaa" FontSize="12" Margin="0,0,0,4" />
                <local:FilePicker x:Name="InstallLocationPicker"
                                  xmlns:local="clr-namespace:Wabbajack"
                                  Icon="Folder" />
            </StackPanel>
            <StackPanel Margin="0,0,0,16">
                <TextBlock Text="Download Location" FontWeight="SemiBold"
                           Foreground="#aaa" FontSize="12" Margin="0,0,0,4" />
                <local:FilePicker x:Name="DownloadLocationPicker"
                                  xmlns:local="clr-namespace:Wabbajack"
                                  Icon="Folder" />
            </StackPanel>

            <Separator Margin="0,0,0,16" Background="#333" />

            <!-- Summary bar -->
            <Border x:Name="SummaryBar" CornerRadius="6" Padding="14"
                    Margin="0,0,0,14">
                <StackPanel>
                    <TextBlock x:Name="SummaryText" FontSize="14" FontWeight="SemiBold" />
                    <TextBlock x:Name="SummarySubText" FontSize="12" Margin="0,2,0,0"
                               Visibility="Collapsed" />
                </StackPanel>
            </Border>

            <!-- Failed checks list -->
            <ItemsControl x:Name="FailedChecksList">
                <ItemsControl.ItemTemplate>
                    <DataTemplate DataType="{x:Type preflight:IPreflightCheck}">
                        <Border CornerRadius="6" Padding="14" Margin="0,0,0,8"
                                Background="#1e1118" BorderBrush="#7f1d1d"
                                BorderThickness="1">
                            <StackPanel>
                                <!-- Title row with action button -->
                                <DockPanel>
                                    <Button DockPanel.Dock="Right"
                                            Content="{Binding ActionLabel}"
                                            Command="{Binding ActionCommand}"
                                            Visibility="{Binding ActionCommand, Converter={StaticResource NullToVisibilityConverter}}"
                                            Padding="12,6" />
                                    <TextBlock FontWeight="SemiBold" Foreground="#f87171"
                                               FontSize="13">
                                        <Run Text="&#10007; " />
                                        <Run Text="{Binding Title, Mode=OneWay}" />
                                    </TextBlock>
                                </DockPanel>

                                <!-- Failure message -->
                                <TextBlock Text="{Binding FailureMessage}" Foreground="#999"
                                           FontSize="12" Margin="0,4,0,0"
                                           TextWrapping="Wrap" />

                                <!-- Sub-items (manual downloads) -->
                                <ItemsControl ItemsSource="{Binding SubItems}"
                                              Margin="0,8,0,0"
                                              Visibility="{Binding SubItems, Converter={StaticResource NullToVisibilityConverter}}">
                                    <ItemsControl.ItemTemplate>
                                        <DataTemplate DataType="{x:Type preflight:PreflightSubItem}">
                                            <StackPanel>
                                                <DockPanel Margin="0,4">
                                                    <Button DockPanel.Dock="Right"
                                                            Content="{Binding ActionLabel}"
                                                            Command="{Binding ActionCommand}"
                                                            Visibility="{Binding ActionCommand, Converter={StaticResource NullToVisibilityConverter}}"
                                                            FontSize="12" Padding="8,4" />
                                                    <StackPanel Orientation="Horizontal">
                                                        <TextBlock Text="{Binding Name}"
                                                                   Foreground="#ddd" FontSize="12" />
                                                        <TextBlock Text="{Binding SizeText}"
                                                                   Foreground="#666" FontSize="11"
                                                                   Margin="8,0,0,0" />
                                                    </StackPanel>
                                                </DockPanel>
                                                <!-- Verification progress bar -->
                                                <StackPanel Visibility="{Binding StatusText, Converter={StaticResource NullToVisibilityConverter}}"
                                                            Margin="0,0,0,4">
                                                    <TextBlock Text="{Binding StatusText}"
                                                               Foreground="#fbbf24" FontSize="12" />
                                                    <ProgressBar Value="{Binding Progress}"
                                                                 Maximum="1" Height="3"
                                                                 Margin="0,4,0,0" />
                                                </StackPanel>
                                            </StackPanel>
                                        </DataTemplate>
                                    </ItemsControl.ItemTemplate>
                                </ItemsControl>
                            </StackPanel>
                        </Border>
                    </DataTemplate>
                </ItemsControl.ItemTemplate>
            </ItemsControl>

            <!-- Install button -->
            <Button x:Name="InstallButton" HorizontalAlignment="Center"
                    Margin="0,16,0,0" Padding="24,10" FontSize="15"
                    FontWeight="SemiBold" Content="Install" />
        </StackPanel>
    </ScrollViewer>
</rxui:ReactiveUserControl>
```

Note: The XAML above references a `NullToVisibilityConverter`. Check if one already exists in the project at `Wabbajack.App.Wpf/Converters/`. If not, create a simple one that returns `Visible` when the bound value is non-null and `Collapsed` when null. Also, the exact style references (`StaticResource TitleStyle`, etc.) and namespace prefixes should be verified against the existing XAML files during implementation.

- [ ] **Step 2: Create the code-behind**

```csharp
// Wabbajack.App.Wpf/Views/Preflight/PreflightView.xaml.cs
using System.Reactive.Disposables;
using System.Windows;
using System.Windows.Media;
using ReactiveUI;
using Wabbajack.Preflight;

namespace Wabbajack.Views.Preflight;

public partial class PreflightView : ReactiveUserControl<PreflightViewModel>
{
    public PreflightView()
    {
        InitializeComponent();

        this.WhenActivated(disposables =>
        {
            // Header
            this.OneWayBind(ViewModel, vm => vm.ModlistName, v => v.ModlistTitle.Text)
                .DisposeWith(disposables);
            this.OneWayBind(ViewModel, vm => vm.ModlistAuthor, v => v.ModlistMeta.Text,
                author => $"by {author}")
                .DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.ViewReadmeCommand, v => v.ViewReadmeButton)
                .DisposeWith(disposables);

            // Summary bar
            this.WhenAnyValue(v => v.ViewModel!.AllPassed, v => v.ViewModel!.PassedCount, v => v.ViewModel!.TotalCount)
                .Subscribe(tuple =>
                {
                    var (allPassed, passed, total) = tuple;
                    if (allPassed)
                    {
                        SummaryBar.Background = new SolidColorBrush(Color.FromRgb(0x1a, 0x2a, 0x1a));
                        SummaryText.Foreground = new SolidColorBrush(Color.FromRgb(0x4a, 0xde, 0x80));
                        SummaryText.Text = $"\u2713 All {total} checks passed";
                        SummarySubText.Text = "Ready to install";
                        SummarySubText.Foreground = new SolidColorBrush(Color.FromRgb(0x86, 0xef, 0xac));
                        SummarySubText.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        SummaryBar.Background = new SolidColorBrush(Color.FromRgb(0x1a, 0x2a, 0x1a));
                        SummaryText.Foreground = new SolidColorBrush(Color.FromRgb(0x4a, 0xde, 0x80));
                        SummaryText.Text = $"\u2713 {passed} of {total} checks passed";
                        SummarySubText.Visibility = Visibility.Collapsed;
                    }
                })
                .DisposeWith(disposables);

            // Failed checks list
            this.OneWayBind(ViewModel, vm => vm.FailedChecks, v => v.FailedChecksList.ItemsSource)
                .DisposeWith(disposables);

            // Install button
            this.BindCommand(ViewModel, vm => vm.InstallCommand, v => v.InstallButton)
                .DisposeWith(disposables);
        });
    }
}
```

- [ ] **Step 3: Verify the project builds**

Run: `dotnet build Wabbajack.App.Wpf`
Expected: Build succeeded. (XAML compilation may produce warnings about design-time data, which is fine.)

- [ ] **Step 4: Commit**

```bash
git add Wabbajack.App.Wpf/Views/Preflight/PreflightView.xaml Wabbajack.App.Wpf/Views/Preflight/PreflightView.xaml.cs
git commit -m "feat: add PreflightView XAML with reactive bindings"
```

---

### Task 9: Wire Preflight into Navigation and InstallationVM

**Files:**
- Modify: `Wabbajack.App.Wpf/ViewModels/Installers/InstallationVM.cs`
- Modify: `Wabbajack.App.Wpf/Views/Installers/InstallationView.xaml`
- Modify: `Wabbajack.App.Wpf/Views/Installers/InstallationView.xaml.cs`

This task integrates the preflight page into the existing install flow. The approach: when a modlist is loaded, the `InstallationVM` creates and hosts a `PreflightViewModel`. The `InstallationView` shows the `PreflightView` during the `Configuration` state, replacing the current readme pane and existing validation. When the user clicks Install in the preflight view, it delegates to the existing `BeginInstall()` flow.

- [ ] **Step 1: Add preflight VM properties to InstallationVM**

In `Wabbajack.App.Wpf/ViewModels/Installers/InstallationVM.cs`, add after the existing reactive properties (around line 130):

```csharp
[Reactive] public partial PreflightViewModel? Preflight { get; set; }
```

Add the using at the top:
```csharp
using Wabbajack.Preflight;
```

- [ ] **Step 2: Create preflight checks in `LoadModlist()`**

In `InstallationVM.LoadModlist()` (around line 640, after metadata is loaded and paths are set), create the preflight VM. Find the Nexus login manager from `_logins`, create the checks, and wire up reactive path changes:

```csharp
// After suggested paths are set, create preflight checks
var nexusLogin = _logins.FirstOrDefault(l => l.LoginFor() == typeof(Wabbajack.Downloaders.NexusDownloader));

var gameFolders = GameRegistry.Games.Keys
    .Select(g => _gameLocator.TryFindLocation(g, out var loc) ? loc : default)
    .Where(p => p != default)
    .ToList();

var gameCheck = new GameInstalledCheck(_gameLocator, ModList.GameType);
var pathCheck = new PathValidationCheck();
var diskCheck = new DiskSpaceCheck();
var nexusCheck = new NexusLoginCheck(nexusLogin!);

var systemDownloads = (AbsolutePath)Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

var downloadsCheck = new DownloadsCheck(
    ModList.Archives,
    Installer.DownloadLocation.TargetPath,
    systemDownloads,
    isPremium: nexusLogin?.LoggedIn == true); // Will be refined by NexusApi.IsPremium() check

_ = downloadsCheck.ScanExistingFiles(CancellationToken.None);

var checks = new IPreflightCheck[] { gameCheck, pathCheck, diskCheck, nexusCheck, downloadsCheck };

Preflight?.Dispose();
Preflight = new PreflightViewModel(checks)
{
    ModlistName = ModList.Name,
    ModlistVersion = ModList.Version?.ToString() ?? "",
    ModlistAuthor = ModList.Author,
    ReadmeUrl = ModList.Readme
};

// Wire path changes to reactive checks
this.WhenAnyValue(
    vm => vm.Installer.Location.TargetPath,
    vm => vm.Installer.DownloadLocation.TargetPath)
    .Subscribe(paths =>
    {
        var (installPath, downloadPath) = paths;
        pathCheck.Update(installPath, downloadPath, gameFolders);

        var metadata = _modlistMetadata?.DownloadMetadata;
        diskCheck.Update(installPath, downloadPath,
            metadata?.SizeOfInstalledFiles ?? 0,
            metadata?.SizeOfArchives ?? 0,
            downloadsCheck.PresentArchiveSize);
    });

// Wire install command from preflight to existing flow
Preflight.InstallCommand = ReactiveCommand.Create(
    () => BeginInstall().FireAndForget(),
    Preflight.WhenAnyValue(p => p.AllPassed));
```

Note: The exact insertion point and variable names should be adjusted based on reading the actual `LoadModlist()` method during implementation. The key is to place this after `ModList` and path suggestions are set.

- [ ] **Step 3: Remove the WebView2 readme pane from InstallationView**

In `Wabbajack.App.Wpf/Views/Installers/InstallationView.xaml`, find the `ReadmeBrowserGrid` (around line 335) and remove it. Replace it with a `ContentControl` that hosts the `PreflightView`:

```xml
<!-- Replace ReadmeBrowserGrid with PreflightView host -->
<ContentControl x:Name="PreflightHost" Grid.Row="1" Margin="0,0,0,16" />
```

In the code-behind (`InstallationView.xaml.cs`), bind the preflight VM:

```csharp
// In WhenActivated, add:
this.WhenAnyValue(v => v.ViewModel!.Preflight)
    .Subscribe(preflight =>
    {
        if (preflight != null)
        {
            var view = new Views.Preflight.PreflightView { ViewModel = preflight };
            PreflightHost.Content = view;
        }
    })
    .DisposeWith(disposables);
```

Remove the `TakeWebViewOwnershipForReadme()` call and related WebView2 readme logic from the code-behind.

- [ ] **Step 4: Remove `PrepareDownloaders()` call from `BeginInstall()`**

In `InstallationVM.BeginInstall()` (around line 701), remove or comment out the `await PrepareDownloaders()` call. The preflight checks now handle login and download validation before install begins.

- [ ] **Step 5: Verify the project builds and runs**

Run: `dotnet build Wabbajack.App.Wpf`
Expected: Build succeeded.

- [ ] **Step 6: Commit**

```bash
git add Wabbajack.App.Wpf/ViewModels/Installers/InstallationVM.cs Wabbajack.App.Wpf/Views/Installers/InstallationView.xaml Wabbajack.App.Wpf/Views/Installers/InstallationView.xaml.cs
git commit -m "feat: wire preflight checks into installation flow, remove readme pane"
```

---

### Task 10: Disk Space Polling Timer

**Files:**
- Modify: `Wabbajack.App.Wpf/ViewModels/Installers/InstallationVM.cs`

The disk space check needs periodic re-evaluation since `DriveInfo.AvailableFreeSpace` isn't observable.

- [ ] **Step 1: Add a 5-second polling timer in the preflight setup code**

In the preflight creation code added in Task 9, after the path change subscription, add:

```csharp
// Poll disk space every 5 seconds
Observable.Interval(TimeSpan.FromSeconds(5))
    .ObserveOn(RxApp.MainThreadScheduler)
    .Subscribe(_ =>
    {
        var installPath = Installer.Location.TargetPath;
        var downloadPath = Installer.DownloadLocation.TargetPath;
        var metadata = _modlistMetadata?.DownloadMetadata;
        diskCheck.Update(installPath, downloadPath,
            metadata?.SizeOfInstalledFiles ?? 0,
            metadata?.SizeOfArchives ?? 0,
            downloadsCheck.PresentArchiveSize);
    })
    .DisposeWith(/* the preflight's disposable or a local CompositeDisposable */);
```

- [ ] **Step 2: Verify the project builds**

Run: `dotnet build Wabbajack.App.Wpf`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add Wabbajack.App.Wpf/ViewModels/Installers/InstallationVM.cs
git commit -m "feat: add 5-second disk space polling for preflight check"
```

---

### Task 11: Run Full Test Suite

**Files:** None (verification only)

- [ ] **Step 1: Run all preflight tests**

Run: `dotnet test Wabbajack.Test --filter "FullyQualifiedName~Preflight" -v n`
Expected: All tests PASS.

- [ ] **Step 2: Run all tests to check for regressions**

Run: `dotnet test Wabbajack.Test -v n`
Expected: Same pass/fail count as before (150 pass, 3 pre-existing failures in AbsolutePathTests and StandardInstallerTest).

- [ ] **Step 3: Run a full build of the solution**

Run: `dotnet build`
Expected: Build succeeded with no new errors.

- [ ] **Step 4: Commit any final adjustments**

If any test fixes were needed, commit them:
```bash
git add -A
git commit -m "fix: address test feedback from preflight integration"
```
