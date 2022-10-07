using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths;

namespace Wabbajack.VFS;

public class IndexRoot
{
    public static IndexRoot Empty = new();

    public IndexRoot(IReadOnlyList<VirtualFile> aFiles,
        IDictionary<FullPath, VirtualFile> byFullPath,
        ILookup<Hash, VirtualFile> byHash,
        IDictionary<AbsolutePath, VirtualFile> byRoot,
        ILookup<IPath, VirtualFile> byName)
    {
        AllFiles = aFiles;
        ByFullPath = byFullPath;
        ByHash = byHash;
        ByRootPath = byRoot;
        ByName = byName;
    }

    public IndexRoot()
    {
        AllFiles = ImmutableList<VirtualFile>.Empty;
        ByFullPath = new Dictionary<FullPath, VirtualFile>();
        ByHash = EmptyLookup<Hash, VirtualFile>.Instance;
        ByRootPath = new Dictionary<AbsolutePath, VirtualFile>();
        ByName = EmptyLookup<IPath, VirtualFile>.Instance;
    }


    public IReadOnlyList<VirtualFile> AllFiles { get; }
    public IDictionary<FullPath, VirtualFile> ByFullPath { get; }
    public ILookup<Hash, VirtualFile> ByHash { get; }
    public ILookup<IPath?, VirtualFile> ByName { get; set; }
    public IDictionary<AbsolutePath, VirtualFile> ByRootPath { get; }

    public async Task<IndexRoot> Integrate(IEnumerable<VirtualFile> files)
    {
        var allFiles = AllFiles.Concat(files)
            .OrderByDescending(f => f.LastModified)
            .GroupBy(f => f.FullPath)
            .Select(g => g.Last())
            .ToList();

        var byFullPath = Task.Run(() => allFiles.SelectMany(f => f.ThisAndAllChildren)
            .ToDictionary(f => f.FullPath));

        var byHash = Task.Run(() => allFiles.SelectMany(f => f.ThisAndAllChildren)
            .Where(f => f.Hash != default)
            .ToLookup(f => f.Hash));

        var byName = Task.Run(() => allFiles.SelectMany(f => f.ThisAndAllChildren)
            .ToLookup(f => f.Name));

        var byRootPath = Task.Run(() => allFiles.ToDictionary(f => f.AbsoluteName));

        var result = new IndexRoot(allFiles,
            await byFullPath,
            await byHash,
            await byRootPath,
            await byName);
        return result;
    }

    public VirtualFile FileForArchiveHashPath(HashRelativePath argArchiveHashPath)
    {
        var cur = ByHash[argArchiveHashPath.Hash].First(f => f.Parent == null);
        return argArchiveHashPath.Parts.Aggregate(cur,
            (current, itm) => ByName[itm].First(f => f.Parent == current));
    }

    public static class EmptyLookup<TKey, TElement>
    {
        public static ILookup<TKey?, TElement> Instance { get; } =
            Enumerable.Empty<TElement>().ToLookup(x => default(TKey));
    }
}