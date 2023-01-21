using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.Common;
using Wabbajack.Common.FileSignatures;
using Wabbajack.DTOs.Streams;
using Wabbajack.DTOs.Texture;
using Wabbajack.DTOs.Vfs;
using Wabbajack.Hashing.PHash;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;

namespace Wabbajack.VFS;

public class VirtualFile
{
    private static readonly HashSet<Extension> TextureExtensions = new()
        {new Extension(".dds"), new Extension(".tga")};

    private static readonly SignatureChecker DDSSig = new(FileType.DDS);

    private IEnumerable<VirtualFile>? _thisAndAllChildren;

    public IPath Name { get; internal set; }

    public RelativePath RelativeName => (RelativePath) Name;

    public AbsolutePath AbsoluteName => (AbsolutePath) Name;


    public FullPath FullPath { get; private set; }

    public Hash Hash { get; internal set; }
    public ImageState? ImageState { get; internal set; }
    public long Size { get; internal set; }

    public ulong LastModified { get; internal set; }

    public ulong LastAnalyzed { get; internal set; }

    public VirtualFile? Parent { get; internal set; }

    public Context Context { get; set; }

    /// <summary>
    ///     Returns the nesting factor for this file. Native files will have a nesting of 1, the factor
    ///     goes up for each nesting of a file in an archive.
    /// </summary>
    public int NestingFactor
    {
        get
        {
            var cnt = 0;
            var cur = this;
            while (cur != null)
            {
                cnt += 1;
                cur = cur.Parent;
            }

            return cnt;
        }
    }

    public ImmutableList<VirtualFile> Children { get; internal set; } = ImmutableList<VirtualFile>.Empty;

    public bool IsArchive => Children != null && Children.Count > 0;

    public bool IsNative => Parent == null;

    public IEnumerable<VirtualFile> ThisAndAllChildren
    {
        get
        {
            if (_thisAndAllChildren == null)
                _thisAndAllChildren = Children.SelectMany(child => child.ThisAndAllChildren).Append(this).ToList();

            return _thisAndAllChildren;
        }
    }


    /// <summary>
    ///     Returns all the virtual files in the path to this file, starting from the root file.
    /// </summary>
    public IEnumerable<VirtualFile> FilesInFullPath
    {
        get
        {
            var stack = ImmutableStack<VirtualFile>.Empty;
            var cur = this;
            while (cur != null)
            {
                stack = stack.Push(cur);
                cur = cur.Parent;
            }

            return stack;
        }
    }


    public VirtualFile TopParent => IsNative ? this : Parent!.TopParent;


    public T ThisAndAllChildrenReduced<T>(T acc, Func<T, VirtualFile, T> fn)
    {
        acc = fn(acc, this);
        return Children.Aggregate(acc, (current, itm) => itm.ThisAndAllChildrenReduced(current, fn));
    }

    public void ThisAndAllChildrenReduced(Action<VirtualFile> fn)
    {
        fn(this);
        foreach (var itm in Children)
            itm.ThisAndAllChildrenReduced(fn);
    }

    private static VirtualFile ConvertFromIndexedFile(Context context, IndexedVirtualFile file, IPath path,
        VirtualFile vparent, IStreamFactory extractedFile)
    {
        var vself = new VirtualFile
        {
            Context = context,
            Name = path,
            Parent = vparent,
            Size = file.Size,
            LastModified = extractedFile.LastModifiedUtc.AsUnixTime(),
            LastAnalyzed = DateTime.Now.AsUnixTime(),
            Hash = file.Hash,
            ImageState = file.ImageState
        };

        vself.FillFullPath();

        vself.Children = file.Children.Select(f => ConvertFromIndexedFile(context, f, f.Name, vself, extractedFile))
            .ToImmutableList();

        return vself;
    }


    internal IndexedVirtualFile ToIndexedVirtualFile()
    {
        return new IndexedVirtualFile
        {
            Hash = Hash,
            ImageState = ImageState,
            Name = Name,
            Children = Children.Select(c => c.ToIndexedVirtualFile()).ToList(),
            Size = Size
        };
    }

    public static async Task<VirtualFile> Analyze(Context context, VirtualFile? parent,
        IStreamFactory extractedFile,
        IPath relPath, CancellationToken token, int depth = 0, IJob? job = null)
    {
        Hash hash;
        if (extractedFile is NativeFileStreamFactory)
        {
            var absPath = (AbsolutePath) extractedFile.Name;
            hash = await context.HashCache.FileHashCachedAsync(absPath, token);
        }
        else
        {
            await using var hstream = await extractedFile.GetStream();
            if (job != null) 
                job.Size += hstream.Length;
            hash = await hstream.HashingCopy(Stream.Null, token, job);
        }

        var found = await context.VfsCache.Get(hash, extractedFile, token);
        if (found != null)
        {
            var file = ConvertFromIndexedFile(context, found!, relPath, parent!, extractedFile);
            file.Name = relPath;
            return file;
        }

        await using var stream = await extractedFile.GetStream();
        var sig = await FileExtractor.FileExtractor.ArchiveSigs.MatchesAsync(stream);
        stream.Position = 0;

        var self = new VirtualFile
        {
            Context = context,
            Name = relPath,
            Parent = parent!,
            Size = stream.Length,
            LastModified = extractedFile.LastModifiedUtc.AsUnixTime(),
            LastAnalyzed = DateTime.Now.AsUnixTime(),
            Hash = hash
        };


        if (TextureExtensions.Contains(relPath.FileName.Extension) && await DDSSig.MatchesAsync(stream) != null)
            try
            {
                self.ImageState = await context.ImageLoader.Load(stream);
                if (job != null)
                {
                    job.Size += self.Size;
                    await job.Report((int) self.Size, token);
                }

                stream.Position = 0;
            }
            catch (Exception)
            {
            }

        self.FillFullPath(depth);


        // Can't extract, so return
        if (!sig.HasValue ||
            !FileExtractor.FileExtractor.ExtractableExtensions.Contains(relPath.FileName.Extension))
        {
            await context.VfsCache.Put(self.ToIndexedVirtualFile(), token);
            return self;
        }
        
        try
        {
            var list = await context.Extractor.GatheringExtract(extractedFile,
                _ => true,
                async (path, sfactory) => await Analyze(context, self, sfactory, path, token, depth + 1, job),
                token);

            self.Children = list.Values.ToImmutableList();
        }
        catch (EndOfStreamException)
        {
            return self;
        }
        catch (Exception ex)
        {
            context.Logger.LogError(ex, "Error while examining the contents of {path}", relPath.FileName);
            throw;
        }

        await context.VfsCache.Put(self.ToIndexedVirtualFile(), token);
        return self;
    }


    internal void FillFullPath()
    {
        var depth = 0;
        var self = this;
        while (self.Parent != null)
        {
            depth += 1;
            self = self.Parent;
        }

        FillFullPath(depth);
    }

    internal void FillFullPath(int depth)
    {
        if (depth == 0)
        {
            FullPath = new FullPath((AbsolutePath) Name);
        }
        else
        {
            var paths = new RelativePath[depth];
            var self = this;
            for (var idx = depth; idx != 0; idx -= 1)
            {
                paths[idx - 1] = self!.RelativeName;
                self = self.Parent;
            }

            FullPath = new FullPath(self!.AbsoluteName, paths);
        }
    }

    public void Write(BinaryWriter bw)
    {
        bw.Write(Name.ToString() ?? string.Empty);
        bw.Write(Size);
        bw.Write(LastModified);
        bw.Write(LastModified);
        bw.Write((ulong) Hash);
        bw.Write(Children.Count);
        foreach (var child in Children)
            child.Write(bw);
    }

    public static VirtualFile Read(Context context, byte[] data)
    {
        using var ms = new MemoryStream(data);
        using var br = new BinaryReader(ms);
        return Read(context, null, br);
    }

    private static VirtualFile Read(Context context, VirtualFile? parent, BinaryReader br)
    {
        var vf = new VirtualFile
        {
            Name = br.ReadIPath(),
            Size = br.ReadInt64(),
            LastModified = br.ReadUInt64(),
            LastAnalyzed = br.ReadUInt64(),
            Hash = Hash.FromULong(br.ReadUInt64()),
            Context = context,
            Parent = parent,
            Children = ImmutableList<VirtualFile>.Empty
        };
        vf.FullPath = new FullPath(vf.AbsoluteName);
        var children = br.ReadInt32();
        for (var i = 0; i < children; i++)
        {
            var child = Read(context, vf, br, (AbsolutePath) vf.Name, new RelativePath[0]);
            vf.Children = vf.Children.Add(child);
        }

        return vf;
    }

    private static VirtualFile Read(Context context, VirtualFile parent, BinaryReader br, AbsolutePath top,
        RelativePath[] subpaths)
    {
        var name = (RelativePath) br.ReadIPath();
        subpaths = subpaths.Add(name);
        var vf = new VirtualFile
        {
            Name = name,
            Size = br.ReadInt64(),
            LastModified = br.ReadUInt64(),
            LastAnalyzed = br.ReadUInt64(),
            Hash = Hash.FromULong(br.ReadUInt64()),
            Context = context,
            Parent = parent,
            Children = ImmutableList<VirtualFile>.Empty,
            FullPath = new FullPath(top, subpaths)
        };

        var children = br.ReadInt32();
        for (var i = 0; i < children; i++)
        {
            var child = Read(context, vf, br, top, subpaths);
            vf.Children = vf.Children.Add(child);
        }

        return vf;
    }

    public HashRelativePath MakeRelativePaths()
    {
        var paths = new RelativePath[FilesInFullPath.Count() - 1];

        var idx = 0;
        foreach (var itm in FilesInFullPath.Skip(1))
        {
            paths[idx] = (RelativePath) itm.Name;
            idx += 1;
        }

        var path = new HashRelativePath(FilesInFullPath.First().Hash, paths);
        return path;
    }

    public VirtualFile? InSameFolder(RelativePath relativePath)
    {
        var newPath = FullPath.InSameFolder(relativePath);
        return Context.Index.ByFullPath.TryGetValue(newPath, out var found) ? found : null;
    }
}