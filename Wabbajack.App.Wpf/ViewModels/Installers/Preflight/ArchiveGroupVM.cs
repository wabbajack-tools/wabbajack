using System.Reactive;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Wabbajack.Common;

namespace Wabbajack;

/// <summary>
///     The summary row that stands in for one band of a download list. A modlist has thousands of archives and
///     a flat list of finished rows is noise, so the bands the user is not watching stay folded into a count
///     and a total until they ask for them. A plain <see cref="ReactiveObject" />, like the rows it heads.
/// </summary>
public partial class ArchiveGroupVM : ReactiveObject
{
    public ArchiveGroupVM(ArchiveGroup group, string title, bool expanded)
    {
        Group = group;
        Title = title;
        IsExpanded = expanded;
        SummaryText = Describe(0, 0);
        ToggleCommand = ReactiveCommand.Create(() => { IsExpanded = !IsExpanded; });
    }

    public ArchiveGroup Group { get; }
    public string Title { get; }

    [Reactive] public partial bool IsExpanded { get; set; }
    [Reactive] public partial string SummaryText { get; set; }

    public ReactiveCommand<Unit, Unit> ToggleCommand { get; }

    /// <summary>Sampled with the footer rather than driven by events; the totals walk every row.</summary>
    public void Summarize(int count, long bytes)
    {
        SummaryText = Describe(count, bytes);
    }

    private static string Describe(int count, long bytes)
    {
        return count == 0 ? "nothing yet" : $"{count:N0} {(count == 1 ? "file" : "files")}, {bytes.ToFileSizeString()}";
    }
}
