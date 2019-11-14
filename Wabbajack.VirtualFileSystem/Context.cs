using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using Ceras;
using Wabbajack.Common;
using Wabbajack.Common.CSP;
using Directory = Alphaleonis.Win32.Filesystem.Directory;
using File = System.IO.File;
using FileInfo = Alphaleonis.Win32.Filesystem.FileInfo;
using Path = Alphaleonis.Win32.Filesystem.Path;

namespace Wabbajack.VirtualFileSystem
{
    public class Context
    {
        public const ulong FileVersion = 0x02;
        public const string Magic = "WABBAJACK VFS FILE";

        private string _stagingFolder = "vfs_staging";
        public IndexRoot Index { get; private set; } = IndexRoot.Empty;

        public TemporaryDirectory GetTemporaryFolder()
        {
            return new TemporaryDirectory(Path.Combine(_stagingFolder, Guid.NewGuid().ToString()));
        }

        public async Task<IndexRoot> AddRoot(string root)
        {
            if (!Path.IsPathRooted(root))
                throw new InvalidDataException($"Path is not absolute: {root}");

            var filtered = await Index.AllFiles
                .ToChannel()
                .UnorderedPipelineRx(o => o.Where(file => File.Exists(file.Name)))
                .TakeAll();

            var by_path = filtered.ToImmutableDictionary(f => f.Name);

            var results = Channel.Create<VirtualFile>(1024);
            var pipeline = Directory.EnumerateFiles(root, "*", DirectoryEnumerationOptions.Recursive)
                .ToChannel()
                .UnorderedPipeline(results, async f =>
                {
                    if (by_path.TryGetValue(f, out var found))
                    {
                        var fi = new FileInfo(f);
                        if (found.LastModified == fi.LastWriteTimeUtc.Ticks && found.Size == fi.Length)
                            return found;
                    }
                    return await VirtualFile.Analyze(this, null, f, f);
                });

            var all_files = await results.TakeAll();
            
            // Should already be done but let's make the async tracker happy
            await pipeline;

            var new_index = await IndexRoot.Empty.Integrate(filtered.Concat(all_files).ToList());

            lock (this)
                Index = new_index;

            return new_index;
        }

        public async Task WriteToFile(string filename)
        {
            using (var fs = File.OpenWrite(filename))
            using (var bw = new BinaryWriter(fs, Encoding.UTF8, true))
            {
                fs.SetLength(0);

                bw.Write(Encoding.ASCII.GetBytes(Magic));
                bw.Write(FileVersion);
                bw.Write((ulong)Index.AllFiles.Count);

                var sizes = await Index.AllFiles
                    .ToChannel()
                    .UnorderedPipelineSync(f =>
                    {
                        var ms = new MemoryStream();
                        f.Write(ms);
                        return ms;
                    })
                    .Select(async ms =>
                    {
                        var size = ms.Position;
                        ms.Position = 0;
                        bw.Write((ulong)size);
                        await ms.CopyToAsync(fs);
                        return ms.Position;
                    })
                    .TakeAll();
                Utils.Log($"Wrote {fs.Position.ToFileSizeString()} file as vfs cache file {filename}");
            }
        }

        public async Task IntegrateFromFile(string filename)
        {
            using (var fs = File.OpenRead(filename))
            using (var br = new BinaryReader(fs, Encoding.UTF8, true))
            {
                var magic = Encoding.ASCII.GetString(br.ReadBytes(Encoding.ASCII.GetBytes(Magic).Length));
                var file_version = br.ReadUInt64();
                if (file_version != FileVersion || magic != magic)
                    throw new InvalidDataException("Bad Data Format");

                var num_files = br.ReadUInt64();

                var input = Channel.Create<byte[]>(1024);
                var pipeline = input.UnorderedPipelineSync<byte[], VirtualFile>(
                    data => { return VirtualFile.Read(this, data); })
                    .TakeAll();

                Utils.Log($"Loading {num_files} files from {filename}");

                for (ulong idx = 0; idx < num_files; idx ++)
                {
                    var size = br.ReadUInt64();
                    var bytes = new byte[size];
                    await br.BaseStream.ReadAsync(bytes, 0, (int)size);
                    await input.Put(bytes);
                }
                input.Close();

                var files = await pipeline;
                var newIndex = await Index.Integrate(files);
                lock (this)
                    Index = newIndex;
            }
        }

        public Action Stage(IEnumerable<VirtualFile> files)
        {
            var grouped = files.SelectMany(f => f.FilesInFullPath)
                .Distinct()
                .Where(f => f.Parent != null)
                .GroupBy(f => f.Parent)
                .OrderBy(f => f.Key?.NestingFactor ?? 0)
                .ToList();

            var Paths = new List<string>();

            foreach (var group in grouped)
            {
                var tmp_path = Path.Combine(_stagingFolder, Guid.NewGuid().ToString());
                FileExtractor.ExtractAll(group.Key.StagedPath, tmp_path).Wait();
                Paths.Add(tmp_path);
                foreach (var file in group)
                    file.StagedPath = Path.Combine(tmp_path, file.Name);
            }

            return () =>
            {
                Paths.Do(p =>
                {
                    if (Directory.Exists(p)) 
                        Directory.Delete(p, true, true);
                });
            };
        }

        public List<PortableFile> GetPortableState(IEnumerable<VirtualFile> files)
        {
            return files.SelectMany(f => f.FilesInFullPath)
                .Distinct()
                .Select(f => new PortableFile()
                {
                    Name = f.Parent != null ? f.Name : null,
                    Hash = f.Hash,
                    ParentHash = f.Parent?.Hash,
                    Size = f.Size
                }).ToList();
        }

        public async Task IntegrateFromPortable(List<PortableFile> state, Dictionary<string, string> links)
        {
            var indexed_state = state.GroupBy(f => f.ParentHash).ToDictionary(f => f.Key ?? "", f => (IEnumerable<PortableFile>)f);
            var parents = await indexed_state[""]
                .ToChannel()
                .UnorderedPipelineSync(f => VirtualFile.CreateFromPortable(this, indexed_state, links, f))
                .TakeAll();

            var new_index = await Index.Integrate(parents);
            lock (this)
                Index = new_index;
        }
    }

    public class IndexRoot
    {
        public static IndexRoot Empty = new IndexRoot();

        public ImmutableList<VirtualFile> AllFiles { get; }
        public ImmutableDictionary<string, VirtualFile> ByFullPath { get; }
        public ImmutableDictionary<string, ImmutableStack<VirtualFile>> ByHash { get; }
        public ImmutableDictionary<string, VirtualFile> ByRootPath { get; }

        public async Task<IndexRoot> Integrate(List<VirtualFile> files)
        {
            var all_files = AllFiles.Concat(files).GroupBy(f => f.Name).Select(g => g.Last()).ToImmutableList();

            var by_full_path = Task.Run(() =>
                all_files.SelectMany(f => f.ThisAndAllChildren)
                     .ToImmutableDictionary(f => f.FullPath));

            var by_hash = Task.Run(() =>
                all_files.SelectMany(f => f.ThisAndAllChildren)
                     .ToGroupedImmutableDictionary(f => f.Hash));

            var by_root_path = Task.Run(() => all_files.ToImmutableDictionary(f => f.Name));
              
            return new IndexRoot(all_files,
                                 await by_full_path,
                                 await by_hash,
                                 await by_root_path);
        }

        public IndexRoot(ImmutableList<VirtualFile> aFiles,
                         ImmutableDictionary<string, VirtualFile> byFullPath,
                         ImmutableDictionary<string, ImmutableStack<VirtualFile>> byHash,
                         ImmutableDictionary<string, VirtualFile> byRoot)
        {
            AllFiles = aFiles;
            ByFullPath = byFullPath;
            ByHash = byHash;
            ByRootPath = byRoot;
        }

        public IndexRoot()
        {
            AllFiles = ImmutableList<VirtualFile>.Empty;
            ByFullPath = ImmutableDictionary<string, VirtualFile>.Empty;
            ByHash = ImmutableDictionary<string, ImmutableStack<VirtualFile>>.Empty;
            ByRootPath = ImmutableDictionary<string, VirtualFile>.Empty;
        }
    }

    public class TemporaryDirectory : IDisposable
    {
        public string FullName { get; }

        public TemporaryDirectory(string name)
        {
            FullName = name;
        }

        public void Dispose()
        {
            Directory.Delete(FullName, true, true);
        }
    }
}
