using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Configuration;
using Wabbajack.BuildServer.Models;
using Wabbajack.Common;
using Wabbajack.VirtualFileSystem;

namespace Wabbajack.BuildServer.Model.Models
{
    public class SqlService
    {
        private IConfiguration _configuration;
        private IDbConnection _conn;

        public SqlService(AppSettings configuration)
        {
            _conn = new SqlConnection(configuration.SqlConnection);
            _conn.Open();
        }

        public IDbConnection Connection { get => _conn; }

        public async Task MergeVirtualFile(VirtualFile vfile)
        {
            var files = new List<IndexedFile>();
            var contents = new List<ArchiveContent>();
            
            IngestFile(vfile, files, contents);

            files = files.DistinctBy(f => f.Hash).ToList();
            contents = contents.DistinctBy(c => (c.Parent, c.Path)).ToList();

            await Connection.ExecuteAsync("dbo.MergeIndexedFiles", new {Files = files.ToDataTable(), Contents = contents.ToDataTable()},
                commandType: CommandType.StoredProcedure);
        }
        
        private static void IngestFile(VirtualFile root, ICollection<IndexedFile> files, ICollection<ArchiveContent> contents)
        {
            var hash = BitConverter.ToInt64(root.Hash.FromBase64());
            files.Add(new IndexedFile
            {
                Hash = hash,
                Sha256 = root.ExtendedHashes.SHA256.FromHex(),
                Sha1 = root.ExtendedHashes.SHA1.FromHex(),
                Md5 = root.ExtendedHashes.MD5.FromHex(),
                Crc32 = BitConverter.ToInt32(root.ExtendedHashes.CRC.FromHex()),
                Size = root.Size
            });

            if (root.Children == null) return;

            foreach (var child in root.Children)
            {
                IngestFile(child, files, contents);

                var child_hash = BitConverter.ToInt64(child.Hash.FromBase64());
                contents.Add(new ArchiveContent
                {
                    Parent = hash,
                    Child = child_hash,
                    Path = child.Name
                });
            }

        }

        public async Task<bool> HaveIndexdFile(string hash)
        {
            var row = await Connection.QueryAsync(@"SELECT * FROM IndexedFile WHERE Hash = @Hash",
                new {Hash = BitConverter.ToInt64(hash.FromBase64())});
            return row.Any();
        }
        
        
                    
        class ArchiveContentsResult
        {
            public long Parent { get; set; }
            public long Hash { get; set; }
            public long Size { get; set; }
            public string Path { get; set; }
        }

        
        /// <summary>
        /// Get the name, path, hash and size of the file with the provided hash, and all files perhaps
        /// contained inside this file. Note: files themselves do not have paths, so the top level result
        /// will have a null path
        /// </summary>
        /// <param name="hash">The xxHash64 of the file to look up</param>
        /// <returns></returns>
        public async Task<IndexedVirtualFile> AllArchiveContents(long hash)
        {

            var files = await Connection.QueryAsync<ArchiveContentsResult>(@"
              SELECT 0 as Parent, i.Hash, i.Size, null as Path FROM IndexedFile WHERE Hash = @Hash
              UNION ALL
              SELECT a.Parent, i.Hash, i.Size, a.Path FROM AllArchiveContent a 
              LEFT JOIN IndexedFile i ON i.Hash = a.Child
              WHERE TopParent = @Hash",
                new {Hash = hash});

            var grouped = files.GroupBy(f => f.Parent).ToDictionary(f => f.Key, f=> (IEnumerable<ArchiveContentsResult>)f);

            List<IndexedVirtualFile> Build(long parent)
            {
                return grouped[parent].Select(f => new IndexedVirtualFile
                {
                    Name = f.Path,
                    Hash = BitConverter.GetBytes(f.Hash).ToBase64(),
                    Size = f.Size,
                    Children = Build(f.Hash)
                }).ToList();
            }
            return Build(0).First();
        }
    }
}
