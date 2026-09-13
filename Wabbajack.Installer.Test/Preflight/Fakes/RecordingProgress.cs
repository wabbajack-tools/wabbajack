#nullable enable
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.Installer.Preflight;

namespace Wabbajack.Installer.Test.Preflight.Fakes;

public sealed class RecordingProgress : IPreflightProgress
{
    public ConcurrentQueue<(long Current, long Total, string? Text)> Reports { get; } = new();
    public ConcurrentQueue<(Archive Archive, ArchiveState State, string? Message, long? Bytes)> Archives { get; } = new();
    public ConcurrentQueue<IReadOnlyList<(Archive Archive, ManualDownloadTarget Target)>> ManualQueues { get; } = new();

    public void Report(long current, long total, string? text = null)
    {
        Reports.Enqueue((current, total, text));
    }

    public void Archive(Archive archive, ArchiveState state, string? message = null, long? bytes = null)
    {
        Archives.Enqueue((archive, state, message, bytes));
    }

    public void ManualQueue(IReadOnlyList<(Archive Archive, ManualDownloadTarget Target)> queue)
    {
        ManualQueues.Enqueue(queue);
    }

    /// <summary>The last state reported for each archive name.</summary>
    public Dictionary<string, ArchiveState> LastStates()
    {
        return Archives.GroupBy(a => a.Archive.Name)
            .ToDictionary(g => g.Key, g => g.Last().State);
    }

    /// <summary>The last message reported for an archive name, or null.</summary>
    public string? LastMessage(string name)
    {
        return Archives.Where(a => a.Archive.Name == name).Select(a => a.Message).LastOrDefault();
    }
}
