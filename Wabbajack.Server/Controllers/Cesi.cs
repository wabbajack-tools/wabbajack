using cesi.DTOs;
using CouchDB.Driver;
using CouchDB.Driver.Views;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Wabbajack.Common;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.DTOs.Texture;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths;
using Wabbajack.VFS;

namespace Wabbajack.Server.Controllers;

[Route("/cesi")]
public class Cesi : ControllerBase 
{
    private readonly ILogger<Cesi> _logger;
    private readonly ICouchDatabase<Analyzed> _db;
    private readonly DTOSerializer _dtos;

    public Cesi(ILogger<Cesi> logger, ICouchDatabase<Analyzed> db, DTOSerializer serializer)
    {
        _logger = logger;
        _db = db;
        _dtos = serializer;
    }

    [HttpGet("entry/{hash}")]
    public async Task<IActionResult> Entry(string hash)
    {
        return Ok(await _db.FindAsync(hash));
    }

    [HttpGet("vfs/{hash}")]
    public async Task<IActionResult> Vfs(string hash)
    {
        var entry = await _db.FindAsync(ReverseHash(hash));
        if (entry == null) return NotFound(new {Message = "Entry not found", Hash = hash, ReverseHash = ReverseHash(hash)});


        var indexed = new IndexedVirtualFile
        {
            Hash = Hash.FromHex(ReverseHash(entry.xxHash64)),
            Size = entry.Size,
            ImageState = GetImageState(entry),
            Children = await GetChildrenState(entry),
        };
        
        
        return Ok(_dtos.Serialize(indexed, true));
    }

    private async Task<List<IndexedVirtualFile>> GetChildrenState(Analyzed entry)
    {
        if (entry.Archive == null) return new List<IndexedVirtualFile>();

        var children = await _db.GetViewAsync<string, Analyzed>("Indexes", "ArchiveContents", new CouchViewOptions<string>
        {
            IncludeDocs = true,
            Key = entry.xxHash64
        });
        
        var indexed = children.ToLookup(d => d.Document.xxHash64, v => v.Document);

        return await entry.Archive.Entries.SelectAsync(async e =>
        {
            var found = indexed[e.Value].First();
            return new IndexedVirtualFile
            {
                Name = e.Key.ToRelativePath(),
                Size = found.Size,
                Hash = Hash.FromHex(ReverseHash(found.xxHash64)),
                ImageState = GetImageState(found),
                Children = await GetChildrenState(found),
            };

        }).ToList();
    }

    private ImageState? GetImageState(Analyzed entry)
    {
        if (entry.DDS == null) return null;
        return new ImageState
        {
            Width = entry.DDS.Width,
            Height = entry.DDS.Height,
            Format = Enum.Parse<DXGI_FORMAT>(entry.DDS.Format),
            PerceptualHash = new PHash(entry.DDS.PHash.FromHex())
        };
    }


    private Hash ReverseHash(Hash hash)
    {
        return Hash.FromHex(hash.ToArray().Reverse().ToArray().ToHex());
    }
    private string ReverseHash(string hash)
    {
        return hash.FromHex().Reverse().ToArray().ToHex();
    }


}