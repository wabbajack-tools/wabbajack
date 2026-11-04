using System;
using System.Collections.Generic;

namespace Wabbajack.Compiler;

/// <summary>
///     Chooses which archive each file is taken from so the list needs as few archives as it can.
///
///     A file that several archives contain can come from any of them, but every archive a list names is one
///     more download for every user, and one more thing that can disappear from its host. Choosing per file as
///     files are matched, in no particular order, picks up archives that nothing else needs. Which choice is
///     right depends on files not seen yet, which makes this set cover, so it is decided once all files are
///     matched:
///
///     1. An archive that is the only source of some file, or that something other than a plain copy already
///        uses, is needed whatever happens. Every file it can supply comes from it.
///     2. What is left is covered greedily: repeatedly take the archive that supplies the most files still
///        without a source, the smaller download on a tie. Greedy set cover is within a log factor of the best
///        answer and in practice usually finds it.
///
///     Work and memory are proportional to the number of (file, candidate archive) pairs. The priority queue is
///     lazy: counts only go down, so an archive's stale count is only corrected when it reaches the top.
/// </summary>
public static class ArchiveChooser
{
    /// <param name="candidates">For each file, the archives it can come from, by index into <paramref name="archiveSizes" />. Never empty.</param>
    /// <param name="archiveSizes">Download size of each archive, used to break ties.</param>
    /// <param name="alreadyNeeded">Archives the list needs regardless of these files.</param>
    /// <returns>For each file, the index of the archive it comes from.</returns>
    public static int[] Choose(IReadOnlyList<int[]> candidates, IReadOnlyList<long> archiveSizes,
        ISet<int> alreadyNeeded)
    {
        var chosen = new int[candidates.Count];
        Array.Fill(chosen, -1);

        var needed = new bool[archiveSizes.Count];
        foreach (var archive in alreadyNeeded) needed[archive] = true;
        foreach (var options in candidates)
            if (options.Length == 1)
                needed[options[0]] = true;

        // Files each archive can supply, for the greedy pass.
        var supplies = new List<int>[archiveSizes.Count];
        for (var a = 0; a < supplies.Length; a++) supplies[a] = new List<int>();

        var uncovered = 0;
        for (var file = 0; file < candidates.Count; file++)
        {
            // Candidates come in order of preference, so the first needed one is the best of them.
            var neededOption = Array.FindIndex(candidates[file], a => needed[a]);
            if (neededOption >= 0)
            {
                chosen[file] = candidates[file][neededOption];
                continue;
            }

            uncovered++;
            foreach (var archive in candidates[file]) supplies[archive].Add(file);
        }

        var remaining = new int[archiveSizes.Count];
        var queue = new PriorityQueue<int, (int Files, long Size, int Index)>();
        for (var a = 0; a < supplies.Length; a++)
        {
            remaining[a] = supplies[a].Count;
            if (remaining[a] > 0) queue.Enqueue(a, (-remaining[a], archiveSizes[a], a));
        }

        while (uncovered > 0)
        {
            var archive = queue.Dequeue();
            if (remaining[archive] == 0) continue;

            // A count that has dropped since it was queued goes back in at its real position.
            queue.TryPeek(out _, out var next);
            if (queue.Count > 0 && (-remaining[archive], archiveSizes[archive], archive).CompareTo(next) > 0)
            {
                queue.Enqueue(archive, (-remaining[archive], archiveSizes[archive], archive));
                continue;
            }

            foreach (var file in supplies[archive])
            {
                if (chosen[file] != -1) continue;
                chosen[file] = archive;
                uncovered--;
                foreach (var other in candidates[file]) remaining[other]--;
            }
        }

        return chosen;
    }
}
