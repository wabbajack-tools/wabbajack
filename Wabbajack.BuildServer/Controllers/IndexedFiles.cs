using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
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
using Wabbajack.BuildServer.Models.JobQueue;
using Wabbajack.BuildServer.Models.Jobs;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Lib.Downloaders;
using Wabbajack.VirtualFileSystem;
using IndexedFile = Wabbajack.BuildServer.Models.IndexedFile;

namespace Wabbajack.BuildServer.Controllers
{
    [Route("/indexed_files")]
    public class IndexedFiles : AControllerBase<IndexedFiles>
    {
        private SqlService _sql;

        public IndexedFiles(ILogger<IndexedFiles> logger, DBContext db, SqlService sql) : base(logger, db, sql)
        {
            _sql = sql;
        }

        [HttpGet]
        [Route("{xxHashAsBase64}/meta.ini")]
        public async Task<IActionResult> GetFileMeta(string xxHashAsBase64)
        {
            var id = Hash.FromHex(xxHashAsBase64);
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

        [HttpPost]
        [Route("notify")]
        public async Task<IActionResult> Notify()
        {
            Utils.Log("Starting ingestion of uploaded INIs");
            var body = await Request.Body.ReadAllAsync();
            await using var ms = new MemoryStream(body);
            using var za = new ZipArchive(ms, ZipArchiveMode.Read);
            int enqueued = 0;
            foreach (var entry in za.Entries)
            {
                await using var ins = entry.Open();
                var iniString = Encoding.UTF8.GetString(await ins.ReadAllAsync());
                var data = (AbstractDownloadState)(await DownloadDispatcher.ResolveArchive(iniString.LoadIniString()));
                if (data == null)
                {
                    Utils.Log("No valid INI parser for: \n" + iniString);
                    continue;
                }
                
                if (data is ManualDownloader.State)
                    continue;

                var key = data.PrimaryKeyString;
                var found = await Db.DownloadStates.AsQueryable().Where(f => f.Key == key).Take(1).ToListAsync();
                if (found.Count > 0)
                    continue;

                await Db.Jobs.InsertOneAsync(new Job
                {
                    Priority = Job.JobPriority.Low,
                    Payload = new IndexJob()
                    {
                        Archive = new Archive
                        {
                            Name = entry.Name,
                            State = data
                        }
                    }
                });
                enqueued += 1;
            }

            Utils.Log($"Enqueued {enqueued} out of {za.Entries.Count} entries from uploaded ini package");

            return Ok(enqueued.ToString());
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
