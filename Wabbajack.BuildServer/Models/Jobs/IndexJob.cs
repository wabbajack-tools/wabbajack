using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using Wabbajack.BuildServer.Models;
using Wabbajack.BuildServer.Models.JobQueue;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Lib.Downloaders;
using Wabbajack.VirtualFileSystem;

namespace Wabbajack.BuildServer.Models.Jobs
{ 
    
    public class IndexJob : AJobPayload
    {
        public Archive Archive { get; set; }
        public override string Description { get; } = "Validate and index an archive";
        public override bool UsesNexus { get => Archive.State is NexusDownloader.State; }
        public override async Task<JobResult> Execute(DBContext db)
        {
            
            /*

            var pk = new List<object>();
            pk.Add(AbstractDownloadState.TypeToName[Archive.State.GetType()]);
            pk.AddRange(Archive.State.PrimaryKey);
            var pk_str = string.Join("|",pk.Select(p => p.ToString()));

            var found = await db.DownloadStates.AsQueryable().Where(f => f.Key == pk_str).Take(1).ToListAsync();
            if (found.Count > 0)
                return JobResult.Success();

            string fileName = Archive.Name;
            string folder = Guid.NewGuid().ToString();
            Utils.Log($"Indexer is downloading {fileName}");
            var downloadDest = Path.Combine(Server.Config.Indexer.DownloadDir, folder, fileName);
            await Archive.State.Download(downloadDest);

            using (var queue = new WorkQueue())
            {
                var vfs = new Context(queue, true);
                await vfs.AddRoot(Path.Combine(Server.Config.Indexer.DownloadDir, folder));
                var archive = vfs.Index.ByRootPath.First();
                var converted = ConvertArchive(new List<IndexedFile>(), archive.Value);
                try
                {
                    await db.IndexedFiles.InsertManyAsync(converted, new InsertManyOptions {IsOrdered = false});
                }
                catch (MongoBulkWriteException)
                {
                }

                await db.DownloadStates.InsertOneAsync(new DownloadState
                {
                    Key = pk_str, 
                    Hash = archive.Value.Hash, 
                    State = Archive.State, 
                    IsValid = true
                });

                var to_path = Path.Combine(Server.Config.Indexer.ArchiveDir,
                    $"{Path.GetFileName(fileName)}_{archive.Value.Hash.FromBase64().ToHex()}_{Path.GetExtension(fileName)}");
                if (File.Exists(to_path))
                    File.Delete(downloadDest);
                else
                    File.Move(downloadDest, to_path);
                Utils.DeleteDirectory(Path.Combine(Server.Config.Indexer.DownloadDir, folder));
            }
*/
            return JobResult.Success();
        }

        private List<IndexedFile> ConvertArchive(List<IndexedFile> files, VirtualFile file, bool isTop = true)
        {
            var name = isTop ? Path.GetFileName(file.Name) : file.Name;
            var ifile = new IndexedFile
            {
                Hash = file.Hash,
                SHA256 = file.ExtendedHashes.SHA256,
                SHA1 = file.ExtendedHashes.SHA1,
                MD5 = file.ExtendedHashes.MD5,
                CRC = file.ExtendedHashes.CRC,
                Size = file.Size,
                Children = file.Children != null ? file.Children.Select(
                    f =>
                    {
                        ConvertArchive(files, f, false);

                        return new ChildFile
                        {
                            Hash = f.Hash,
                            Name = f.Name.ToLowerInvariant(),
                            Extension = Path.GetExtension(f.Name.ToLowerInvariant())
                        };
                    }).ToList() : new List<ChildFile>()
            };
            ifile.IsArchive = ifile.Children.Count > 0;
            files.Add(ifile);
            return files;
        }


    }
    
}
