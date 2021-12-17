using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using FluentFTP.Helpers;
using Microsoft.Extensions.Logging;
using Wabbajack.BuildServer;
using Wabbajack.Common;
using Wabbajack.DTOs.CDN;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;


namespace Wabbajack.Server.DataModels;

public class AuthorFiles
{
    private readonly ILogger<AuthorFiles> _logger;
    private readonly AppSettings _settings;
    private readonly DTOSerializer _dtos;
    private Dictionary<string, FileDefinition> _byServerId = new();

    public AbsolutePath AuthorFilesLocation => _settings.AuthoredFilesFolder.ToAbsolutePath();

    public AuthorFiles(ILogger<AuthorFiles> logger, AppSettings settings, DTOSerializer dtos)
    {
        _logger = logger;
        _settings = settings;
        _dtos = dtos;
    }

    public IEnumerable<AbsolutePath> AllDefinitions => AuthorFilesLocation.EnumerateFiles("definition.json.gz");

    public async Task<FileDefinitionMetadata[]> AllAuthoredFiles()
    {
        var defs = new List<FileDefinitionMetadata>();
        foreach (var file in AllDefinitions)
        {
            defs.Add(new FileDefinitionMetadata
            {
                Definition = await ReadDefinition(file),
                Updated = file.LastModifiedUtc()
            });
        }

        _byServerId = defs.ToDictionary(f => f.Definition.ServerAssignedUniqueId!, f => f.Definition);
        return defs.ToArray();
    }

    public async Task<Stream> StreamForPart(string hashAsHex, int part)
    {
        return AuthorFilesLocation.Combine(hashAsHex, "parts", part.ToString()).Open(FileMode.Open);
    }
    
    public async Task<Stream> CreatePart(string mungedName, int part)
    {
        return AuthorFilesLocation.Combine(mungedName, "parts", part.ToString()).Open(FileMode.Create, FileAccess.Write, FileShare.None);
    }

    public async Task WriteDefinition(FileDefinition definition)
    {
        var path = AuthorFilesLocation.Combine(definition.MungedName, "definition.json.gz");
        path.Parent.CreateDirectory();
        path.Parent.Combine("parts").CreateDirectory();
        
        await using var ms = new MemoryStream();
        await using (var gz = new GZipStream(ms, CompressionLevel.Optimal, true))
        {
            await _dtos.Serialize(definition, gz);
        }

        await path.WriteAllBytesAsync(ms.ToArray());
    }

    public async Task<FileDefinition> ReadDefinition(string mungedName)
    {
        return await ReadDefinition(AuthorFilesLocation.Combine(mungedName, "definition.json.gz"));
    }
    
    private async Task<FileDefinition> ReadDefinition(AbsolutePath file)
    {
        var gz = new GZipStream(new MemoryStream(await file.ReadAllBytesAsync()), CompressionMode.Decompress);
        var definition = (await _dtos.DeserializeAsync<FileDefinition>(gz))!;
        return definition;
    }

    public class FileDefinitionMetadata
    {
        public FileDefinition Definition { get; set; }
        public DateTime Updated { get; set; }
        public string HumanSize => Definition.Size.ToFileSizeString();
    }

    public async Task DeleteFile(FileDefinition definition)
    {
        var folder = AuthorFilesLocation.Combine(definition.MungedName);
        folder.DeleteDirectory();
    }

    public async Task<FileDefinition> ReadDefinitionForServerId(string hashAsHex)
    {
        var data = await ReadDefinition(_settings.MirrorFilesFolder.ToAbsolutePath().Combine(hashAsHex).Combine("definition.json.gz"));
        if (data.Hash != Hash.FromHex(hashAsHex))
            throw new Exception($"Definition hex does not match {data.Hash.ToHex()} vs {hashAsHex}");
        return data;
    }
}