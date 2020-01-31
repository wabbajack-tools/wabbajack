using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DynamicData;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using Wabbajack.BuildServer.Model.Models;
using Wabbajack.BuildServer.Models;
using Wabbajack.Common;
using Wabbajack.Lib.Downloaders;
using Wabbajack.VirtualFileSystem;
using IndexedFile = Wabbajack.BuildServer.Models.IndexedFile;

namespace Wabbajack.BuildServer.Controllers
{
    [Route("/indexed_files")]
    public class IndexedFiles : AControllerBase<IndexedFiles>
    {
        private SqlService _sql;

        public IndexedFiles(ILogger<IndexedFiles> logger, DBContext db, SqlService sql) : base(logger, db)
        {
            _sql = sql;
        }

        [HttpGet]
        [Route("{xxHashAsBase64}/meta.ini")]
        public async Task<IActionResult> GetFileMeta(string xxHashAsBase64)
        {
            var id = xxHashAsBase64.FromHex().ToBase64();
            var state = await Db.DownloadStates.AsQueryable()
                .Where(d => d.Hash == id && d.IsValid)
                .OrderByDescending(d => d.LastValidationTime)
                .Take(1)
                .ToListAsync();

            if (state.Count == 0)
                return NotFound();
            Response.ContentType = "text/plain";
            return Ok(string.Join("\r\n", state.FirstOrDefault().State.GetMetaIni()));
        }

        [Authorize]
        [HttpDelete]
        [Route("/indexed_files/nexus/{Game}/mod/{ModId}")]
        public async Task<IActionResult> PurgeBySHA256(string Game, string ModId)
        {
            var files = await Db.DownloadStates.AsQueryable().Where(d => d.State is NexusDownloader.State &&
                                                                   ((NexusDownloader.State)d.State).GameName == Game &&
                                                                   ((NexusDownloader.State)d.State).ModID == ModId)
                .ToListAsync();

            async Task DeleteParentsOf(HashSet<string> acc, string hash)
            {
                var parents = await Db.IndexedFiles.AsQueryable().Where(f => f.Children.Any(c => c.Hash == hash))
                    .ToListAsync();

                foreach (var parent in parents)
                    await DeleteThisAndAllChildren(acc, parent.Hash);
            }
            
            async Task DeleteThisAndAllChildren(HashSet<string> acc, string hash)
            {
                acc.Add(hash);
                var children = await Db.IndexedFiles.AsQueryable().Where(f => f.Hash == hash).FirstOrDefaultAsync();
                if (children == null) return;
                foreach (var child in children.Children)
                {
                    await DeleteThisAndAllChildren(acc, child.Hash);
                }

            }
            
            var acc = new HashSet<string>();
            foreach (var file in files)
                await DeleteThisAndAllChildren(acc, file.Hash);

            var acclst = acc.ToList();
            await Db.DownloadStates.DeleteManyAsync(d => acc.Contains(d.Hash));
            await Db.IndexedFiles.DeleteManyAsync(d => acc.Contains(d.Hash));

            return Ok(acc.ToList());
        }

        [HttpGet]
        [Route("{xxHashAsBase64}")]
        public async Task<IActionResult> GetFile(string xxHashAsBase64)
        {
            var result = await _sql.AllArchiveContents(BitConverter.ToInt64(xxHashAsBase64.FromHex()));
            if (result == null)
                return NotFound();
            return Ok(result);
        }

        public class TreeResult : IndexedFile
        {
            public List<TreeResult> ChildFiles { get; set; }
        }
    }
}
