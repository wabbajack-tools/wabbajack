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
        private AppSettings _settings;

        public IndexedFiles(ILogger<IndexedFiles> logger, DBContext db, SqlService sql, AppSettings settings) : base(logger, db, sql)
        {
            _settings = settings;
            _sql = sql;
        }

        [HttpGet]
        [Route("{xxHashAsBase64}/meta.ini")]
        public async Task<IActionResult> GetFileMeta(string xxHashAsBase64)
        {
            var id = Hash.FromHex(xxHashAsBase64);
            
            var result = await SQL.GetIniForHash(id);
            if (result == null)
                return NotFound();
            
            Response.ContentType = "text/plain";
            return Ok(result);
        }

        [HttpGet]
        [Route("ingest/{folder}")]
        [Authorize]
        public async Task<IActionResult> Ingest(string folder)
        {
            var fullPath = folder.RelativeTo((AbsolutePath)_settings.TempFolder);
            Utils.Log($"Ingesting Inis from {fullPath}");
            int loadCount = 0;
            foreach (var file in fullPath.EnumerateFiles().Where(f => f.Extension == Consts.IniExtension))
            {
                var loaded = (AbstractDownloadState)(await DownloadDispatcher.ResolveArchive(file.LoadIniFile(), true));
                if (loaded == null)
                {
                    Utils.Log($"Unsupported Ini {file}");
                    continue;
                }

                var hash = Hash.FromHex(((string)file.FileNameWithoutExtension).Split("_").First()); 
                await SQL.AddDownloadState(hash, loaded);
                loadCount += 1;
            }

            return Ok(loadCount);
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
                Utils.Log(iniString);
                var data = (AbstractDownloadState)(await DownloadDispatcher.ResolveArchive(iniString.LoadIniString(), true));
                
                if (data == null)
                {
                    Utils.Log("No valid INI parser for: \n" + iniString);
                    continue;
                }
                
                if (data is ManualDownloader.State)
                    continue;

                if (await SQL.HaveIndexedArchivePrimaryKey(data.PrimaryKeyString))
                    continue;

                await SQL.EnqueueJob(new Job
                {
                    Priority = Job.JobPriority.Low,
                    Payload = new IndexJob
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
