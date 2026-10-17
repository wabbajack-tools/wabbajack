using System;
using System.Collections.Generic;
using System.Linq;
using Wabbajack.Installer.Preflight;

namespace Wabbajack.App.Avalonia.ViewModels.Installers.Preflight;

/// <summary>
///     Decides when "open the next one automatically" opens the next manual download's page, and which one. It
///     only ever moves at the user's pace: a new page opens once every page already opened has a file arriving
///     for it (or the user skipped it), so walking away from the browser leaves one page open, not a pile. A wrong
///     file or a failure stops it until the user deals with it. Nothing opens until the user has opened a page
///     themselves, so turning the option on never sends the browser anywhere by surprise.
/// </summary>
public sealed class AutoOpenPlanner
{
    private readonly HashSet<string> _opened = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _released = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>A page was opened for this file, by the user or by this.</summary>
    public void Opened(string key)
    {
        _opened.Add(key);
        _released.Remove(key);
    }

    /// <summary>The user moved past this file without downloading it here (skipped it), so it no longer holds things up.</summary>
    public void Released(string key)
    {
        _released.Add(key);
    }

    /// <summary>The file whose page should open next, or null when nothing should open yet.</summary>
    public string? Next(IEnumerable<ManualDownloadItem> items)
    {
        if (_opened.Count == 0) return null;

        var all = items.ToList();
        var stillWaiting = all.Any(i => _opened.Contains(i.Key) && !_released.Contains(i.Key) &&
                                        i.State is ManualDownloadState.Pending or ManualDownloadState.WrongFile
                                            or ManualDownloadState.Failed);
        if (stillWaiting) return null;

        return all.Where(i => i.State == ManualDownloadState.Pending && !_opened.Contains(i.Key))
            .OrderBy(i => i.Order)
            .FirstOrDefault()?.Key;
    }
}
