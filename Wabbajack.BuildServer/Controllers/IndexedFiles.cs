using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using Wabbajack.BuildServer.Models;
using Wabbajack.Common;
using Wabbajack.Lib.DTOs;

namespace Wabbajack.BuildServer.Controllers
{
    [Route("/indexed_files")]
    public class IndexedFiles : AControllerBase<IndexedFiles>
    {
        public IndexedFiles(ILogger<IndexedFiles> logger, DBContext db) : base(logger, db)
        {
        }

        [HttpGet]
        [Route("{xxHashAsBase64}")]
        public async Task<IndexedVirtualFile> GetFile(string xxHashAsBase64)
        {
            var id = xxHashAsBase64;//.FromHex().ToBase64();
            var query = new[]
            {
                new BsonDocument("$match",
                    new BsonDocument("_id", id)),
                new BsonDocument("$graphLookup",
                    new BsonDocument
                    {
                        {"from", "indexed_files"},
                        {"startWith", "$Children.Hash"},
                        {"connectFromField", "Hash"},
                        {"connectToField", "_id"},
                        {"as", "ChildFiles"},
                        {"maxDepth", 8},
                        {"restrictSearchWithMatch", new BsonDocument()}
                    })
            };

            var result = await Db.IndexedFiles.AggregateAsync<TreeResult>(query);

            IndexedVirtualFile Convert(TreeResult t, string Name = null)
            {
                if (t == null)
                    return null;

                Dictionary<string, TreeResult> indexed_children= new Dictionary<string, TreeResult>();
                if (t.IsArchive) 
                    indexed_children = t.ChildFiles.ToDictionary(t => t.Hash);
                
                var file = new IndexedVirtualFile
                {
                    Name = Name,
                    Size = t.Size,
                    Hash =  t.Hash,
                    Children = t.IsArchive ? t.Children.Select(child => Convert(indexed_children[child.Hash], child.Name)).ToList() : new List<IndexedVirtualFile>()
                };
                return file;
            }

            return Convert(result.FirstOrDefault());
        }

        public class TreeResult : IndexedFile
        {
            public List<TreeResult> ChildFiles { get; set; }
        }


    }
}
