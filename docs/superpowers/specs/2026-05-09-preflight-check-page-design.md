# Preflight Check Page

**Date:** 2026-05-09
**Status:** Draft

## Overview

Replace the current install configuration view with a unified preflight check page. When a user selects a modlist to install, this page validates all prerequisites before enabling the Install button. Checks are fully reactive — they update automatically as conditions change (login state, files appearing, disk space freed).

Passing checks collapse into a single summary line ("4 of 5 checks passed"). Only failing checks are shown individually with actionable UI (buttons, links). The page also includes path configuration inline (install and download locations), a "View Readme" button that opens the system browser, and removes the in-app WebView2 readme pane from this view.

## User Flow

1. User selects a modlist from the gallery or opens a `.wabbajack` file
2. Preflight page appears showing modlist name, version, author, and a "View Readme" button
3. Install and download path pickers are shown at the top
4. Checks begin running immediately
5. Passing checks roll into a green summary bar at the top of the check list
6. Failing checks appear as individual cards below the summary, each with a description and an action (if applicable)
7. As the user resolves issues (logs in, downloads files, frees space), checks update reactively
8. When all checks pass, the Install button enables
9. User clicks Install to begin the standard installation flow

## Layout

```
+----------------------------------------------------------+
| [Modlist Name] v1.2.3 by AuthorName      [View Readme]   |
+----------------------------------------------------------+
| Install Location  [D:\Modlists\LorenahSE         ] [...] |
| Download Location [D:\Modlists\LorenahSE\downloads] [...] |
+----------------------------------------------------------+
| [green] ✓ 3 of 5 checks passed                           |
|                                                           |
| [red card] ✗ Nexus Mods login required         [Log In]  |
|   Log in to download mods automatically                   |
|                                                           |
| [red card] ✗ 2 files need manual download                 |
|   Download these files — they'll be detected automatically|
|   SKSE64_2_00_21.7z    12.4 MB         [Download ↗]      |
|   ENBSeries_v0494.zip   3.1 MB         [Download ↗]      |
|                                                           |
|                    [Install] (disabled)                    |
+----------------------------------------------------------+
```

When all checks pass:

```
+----------------------------------------------------------+
| [green] ✓ All 5 checks passed                            |
|           Ready to install                                |
|                                                           |
|                    [Install] (enabled)                     |
+----------------------------------------------------------+
```

## Preflight Checks

### 1. Game Installed Check

**What:** Verifies the modlist's target game is installed and detectable.

**How:** Calls `IGameLocator.IsInstalled(modlist.GameType)`. If not found, checks `GameMetaData.CommonlyConfusedWith` to provide a helpful hint (e.g., "Skyrim SE is installed, but this list requires Skyrim AE").

**Reactivity:** Checked once on page load. Game installations don't change mid-session. If the user installs the game while the page is open, a manual "Re-check" link on this specific check card re-runs detection.

**Failure display:**
- Title: "Game not found: {GameName}"
- Detail: "Install it via Steam, GOG, or Epic Games Store"
- If confused game found: "Found {ConfusedGame} — this list requires {RequiredGame}"
- No action button (user must install externally)

### 2. Disk Space Check

**What:** Verifies enough free space on the install and download drives.

**How:**
- After the initial archive hash scan, calculates remaining download space needed by subtracting already-present archives from `DownloadMetadata.SizeOfArchives`
- Compares remaining download size against download drive free space
- Compares `DownloadMetadata.SizeOfInstalledFiles` against install drive free space
- If both paths are on the same drive, checks combined space requirement

**Reactivity:** Polls `DriveInfo.AvailableFreeSpace` every 5 seconds. Disk space is not observable via filesystem events.

**Failure display:**
- Title: "Not enough disk space"
- Detail: "Install needs {X} GB, only {Y} GB free on {Drive}:\" (and/or same for downloads)
- No action button (user must free space externally)

### 3. Nexus Mods Login Check

**What:** Verifies the user is logged into Nexus Mods.

**How:** Observes `ITokenProvider<NexusOAuthState>` for token presence. Also exposes whether the user has Nexus Premium, which the Downloads Check uses to determine which files need manual download.

**Reactivity:** Fully reactive via the token provider observable. Updates instantly when the user completes the OAuth flow.

**Failure display:**
- Title: "Nexus Mods login required"
- Detail: "Log in to download mods automatically"
- Action: "Log In" button triggers the existing OAuth PKCE flow via system browser

### 4. Downloads Check

**What:** Verifies all required archives are present in the download folder.

**How — Classification:**
- Hashes all files already in the download folder on page load
- Compares against `ModList.Archives` to find missing archives
- Classifies missing archives by download capability:
  - **Auto-downloadable:** Nexus archives (premium users only) and direct HTTP archives
  - **Manual:** Everything else (MEGA, LoversLab, VectorPlexus, manual sites, and all Nexus archives for non-premium users)
- Auto-downloadable files are not surfaced to the user — they will be downloaded during installation
- Only manual files are shown in the preflight check

**How — File Watching:**
- `FileSystemWatcher` monitors two directories:
  - The user's system Downloads folder (`KnownFolders.Downloads`)
  - The configured modlist download folder
- When a file is created or changed, check if its size matches any missing archive
- If size matches, hash the file in the background
- If hash matches, move the file to the download folder silently
- Update the check status reactively

**How — Hashing feedback:**
- While hashing a candidate file, show an inline progress indicator: "Verifying {filename}..." with a progress bar
- If hash doesn't match, silently discard — don't bother the user

**Reactivity:** Fully reactive via `FileSystemWatcher` events and the hash-scan results.

**Failure display:**
- Title: "{N} files need manual download"
- Detail: "Download these files — they'll be detected automatically"
- Sub-items: list of files, each showing filename, size, and a "Download ↗" link that opens the source URL in the system browser
- Files currently being verified show "Verifying {filename}..." with a progress bar instead of the download link

**When the check passes:**
- All manual files are present and verified
- Check transitions to passed and rolls into the summary

### 5. Path Validation Check

**What:** Validates install and download paths are acceptable (existing checks from current `Validate()` method).

**How:** Runs the same validation rules currently in `InstallationVM.Validate()`:
- Paths have sufficient depth
- Not inside Windows folder
- Install path ≠ download path
- Not nested inside each other
- Not inside any detected game folder
- Not inside Wabbajack's own folder
- Not in special Windows folders

**Reactivity:** Fully reactive — re-evaluates when either path changes.

**Failure display:**
- Title: "Invalid {install/download} path"
- Detail: specific reason (e.g., "Install path cannot be inside a game folder")
- No action button — user changes the path via the picker above

## Architecture

### IPreflightCheck Interface

```csharp
public interface IPreflightCheck : IDisposable
{
    string Title { get; }
    PreflightCheckStatus Status { get; }       // Pending, Checking, Passed, Failed
    string? FailureMessage { get; }
    ICommand? ActionCommand { get; }            // Log In, Re-check, etc.
    string? ActionLabel { get; }                // Button text
    IReadOnlyList<PreflightSubItem>? SubItems { get; } // Manual download list
}

public enum PreflightCheckStatus { Pending, Checking, Passed, Failed }

public class PreflightSubItem : ReactiveObject
{
    public string Name { get; init; }
    public string SizeText { get; init; }
    [Reactive] public partial string? StatusText { get; set; }    // "Verifying..." or null
    [Reactive] public partial double? Progress { get; set; }      // 0.0-1.0 for hash progress, null if not active
    public ICommand? ActionCommand { get; init; }                 // "Open in browser"
    public string? ActionLabel { get; init; }
}
```

All properties are reactive (`INotifyPropertyChanged` / `[Reactive]`). The VM observes them to compute the summary.

### PreflightViewModel

Owns the collection of `IPreflightCheck` instances. Computes:
- `PassedCount` / `TotalCount` for the summary bar
- `FailedChecks` filtered list for display
- `AllPassed` boolean that gates the Install command
- `InstallCommand` — enabled only when `AllPassed && !IsInstalling`

Also owns:
- `ModlistMetadata` (name, version, author, readme URL)
- `InstallPath` and `DownloadPath` (bound to the path pickers)
- `ViewReadmeCommand` — opens readme URL in system browser via `Process.Start`

### Dependency Flow

```
PreflightViewModel
  ├── GameInstalledCheck(IGameLocator, modlist.GameType)
  ├── DiskSpaceCheck(installPath, downloadPath, downloadMetadata, presentArchives)
  ├── NexusLoginCheck(ITokenProvider<NexusOAuthState>)
  ├── DownloadsCheck(modlist.Archives, downloadPath, nexusLoginCheck.IsPremium)
  └── PathValidationCheck(installPath, downloadPath, IGameLocator)
```

The `DownloadsCheck` depends on `NexusLoginCheck.IsPremium` to classify archives. When premium status changes (user logs in), the downloads check re-evaluates which files need manual download.

The `DiskSpaceCheck` depends on `DownloadsCheck.PresentArchivesSize` to calculate remaining download space needed.

### View

Single `PreflightView.xaml` as a `ReactiveUserControl<PreflightViewModel>`. Uses:
- Standard WPF `ItemsControl` for the failed checks list with `DataTemplate` per check type
- MahApps.Metro styling consistent with the rest of the app
- No WebView2 — the readme pane is removed from this view entirely

## What Gets Removed

- The in-app WebView2 readme pane from the install configuration view
- The `PrepareDownloaders()` mid-install login flow (logins happen in preflight now)
- The mid-install `ShowMissingManualReport()` flow (manual downloads happen in preflight now)
- The commented-out disk space validation code in `InstallationVM.Validate()`

## What Stays the Same

- The actual `StandardInstaller.Begin()` execution flow
- Archive downloading during install (for auto-downloadable Nexus/HTTP files)
- All existing downloader implementations
- The OAuth PKCE login flow (just triggered from preflight instead of mid-install)

## Edge Cases

- **User changes paths after checks pass:** All path-dependent checks (disk space, path validation, downloads) re-evaluate reactively.
- **File appears in downloads but is still being written (partial download):** Size won't match until the download completes. `FileSystemWatcher` fires on change events too, so we'll re-check when the file is finalized.
- **Hash mismatch on size-matched file:** Silently ignore. Don't show an error — the user might have an unrelated file of the same size in their Downloads folder.
- **Same file detected in both watched folders:** First match wins. The file in the download folder takes priority (no move needed).
- **Nexus token expires during preflight:** Token provider observable fires, login check transitions to failed, downloads check re-evaluates.
- **Multiple archives with the same size:** When a file of that size appears, hash it and check against all archives of that size. Move only if exactly one hash matches.
