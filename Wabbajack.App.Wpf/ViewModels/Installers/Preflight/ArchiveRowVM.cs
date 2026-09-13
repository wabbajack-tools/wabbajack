using System;
using Humanizer;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Wabbajack.Common;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.Installer.Preflight;

namespace Wabbajack;

/// <summary>
///     One row in an archive list. A plain <see cref="ReactiveObject" /> rather than a <see cref="ViewModel" />:
///     there can be thousands of these and each only needs change notification, not an activator or a
///     loading lock. The identity columns never change; <see cref="State" />, <see cref="Progress" /> and
///     <see cref="Detail" /> are updated in place so a row keeps its instance for its whole life.
/// </summary>
public partial class ArchiveRowVM : ReactiveObject
{
    public ArchiveRowVM(Archive archive, ManualDownloadTarget? target)
    {
        Key = archive.Name;
        Name = archive.Name;
        Size = archive.Size;
        SizeText = archive.Size.ToFileSizeString();
        Url = target?.Url;
        SourceName = target?.SiteName ?? DescribeSource(archive.State);
        Detail = string.Empty;
    }

    public string Key { get; }
    public string Name { get; }
    public long Size { get; }
    public string SizeText { get; }
    public string SourceName { get; }
    public Uri? Url { get; }

    /// <summary>Queue position for manual rows; zero for everything else. Read by the sort, not bound.</summary>
    public int Order { get; private set; }

    [Reactive] public partial ArchiveState State { get; set; }

    /// <summary>0..1, meaningful while the archive is Downloading or being verified.</summary>
    [Reactive] public partial double Progress { get; set; }

    [Reactive] public partial string Detail { get; set; }

    /// <summary>
    ///     Active rows float to the top, finished rows sink. Progress is deliberately not part of the sort
    ///     key so a tick never reorders the list.
    /// </summary>
    public int SortBucket => State switch
    {
        ArchiveState.Downloading or ArchiveState.ManualInProgress => 0,
        ArchiveState.Failed or ArchiveState.Unsupported => 1,
        ArchiveState.Present or ArchiveState.Downloaded => 3,
        _ => 2
    };

    public bool IsDone => State is ArchiveState.Present or ArchiveState.Downloaded;

    public void Apply(ArchiveStatus status)
    {
        var progress = status.State switch
        {
            ArchiveState.Present or ArchiveState.Downloaded => 1d,
            ArchiveState.Downloading or ArchiveState.ManualInProgress when status.Progress is { } bytes && Size > 0 =>
                Math.Clamp((double) bytes / Size, 0d, 1d),
            _ => 0d
        };

        // Order matters: the list re-sorts on State, so set the cheap columns first.
        Progress = progress;
        Detail = status.Message ?? string.Empty;
        State = status.State;
    }

    public void Apply(ManualDownloadItem item)
    {
        var state = item.State switch
        {
            ManualDownloadState.Detected or ManualDownloadState.Waiting or ManualDownloadState.Verifying =>
                ArchiveState.ManualInProgress,
            ManualDownloadState.Moved => ArchiveState.Present,
            ManualDownloadState.WrongFile or ManualDownloadState.Failed => ArchiveState.Failed,
            _ => ArchiveState.ManualRequired
        };

        Order = item.Order;
        Progress = item.State switch
        {
            ManualDownloadState.Moved => 1d,
            ManualDownloadState.Verifying => item.Progress.Value,
            _ => 0d
        };
        Detail = item.Message ?? string.Empty;
        State = state;
    }

    private static string DescribeSource(IDownloadState state)
    {
        return state switch
        {
            GameFileSource => "Game files",
            Bethesda => "Creation Club",
            Nexus => "Nexus Mods",
            Http => "Direct download",
            WabbajackCDN => "Wabbajack CDN",
            _ => state.GetType().Name.Humanize(LetterCasing.Title)
        };
    }
}
