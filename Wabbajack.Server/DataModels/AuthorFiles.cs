using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Compression;
using System.Web;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Microsoft.IO;
using Wabbajack.BuildServer;
using Wabbajack.Common;
using Wabbajack.DTOs.CDN;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;


namespace Wabbajack.Server.DataModels;

public class AuthorFiles
{
    private readonly ILogger<AuthorFiles> _logger;
    private readonly AppSettings _settings;
    private readonly DTOSerializer _dtos;
    private ConcurrentDictionary<string, FileDefinition> _byServerId = new();
    private readonly IAmazonS3 _s3;
    private readonly ConcurrentDictionary<string,FileDefinitionMetadata> _fileCache;
    private readonly string _bucketName;
    private ConcurrentDictionary<RelativePath, long> _allObjects = new();
    private HashSet<RelativePath> _mangledNames;
    private readonly RecyclableMemoryStreamManager _streamPool;
    private readonly HttpClient _httpClient;
    private readonly AbsolutePath _cacheFile;

    private Uri _baseUri => new($"https://r2.wabbajack.org/");
    
    public AuthorFiles(ILogger<AuthorFiles> logger, AppSettings settings, DTOSerializer dtos, IAmazonS3 s3, HttpClient client)
    {
        _httpClient = client;
        _s3 = s3;
        _logger = logger;
        _settings = settings;
        _dtos = dtos;
        _fileCache = new ConcurrentDictionary<string, FileDefinitionMetadata>();
        _bucketName = settings.AuthoredFilesS3.BucketName;
        _ = PrimeCache();
        _streamPool = new RecyclableMemoryStreamManager();
        _cacheFile = _settings.AuthoredFilesS3.BucketCacheFile.ToAbsolutePath();
    }

    private async Task PrimeCache()
    {
        try
        {
            if (!_cacheFile.FileExists())
            {
                var allObjects = await AllObjects().ToArrayAsync();
                foreach (var obje in allObjects)
                {
                    _allObjects.TryAdd(obje.Key.ToRelativePath(), obje.LastModified.ToFileTimeUtc());
                }
                SaveBucketCacheFile(_cacheFile);
            }
            else
            {
                LoadBucketCacheFile(_cacheFile);
            }


            _mangledNames = _allObjects
                .Where(f => f.Key.EndsWith("definition.json.gz"))
                .Select(f => f.Key.Parent)
                .ToHashSet();

            await Parallel.ForEachAsync(_mangledNames, async (name, _) =>
            {
                if (!_allObjects.TryGetValue(name.Combine("definition.json.gz"), out var value))
                    return;

                _logger.LogInformation("Priming {Name}", name);
                var definition = await PrimeDefinition(name);
                var metadata = new FileDefinitionMetadata()
                {
                    Definition = definition,
                    Updated = DateTime.FromFileTimeUtc(value)
                };
                _fileCache.TryAdd(definition.MungedName, metadata);
                _byServerId.TryAdd(definition.ServerAssignedUniqueId!, definition);
            });
            
            _logger.LogInformation("Finished priming cache, {Count} files {Size} GB cached", _fileCache.Count, 
                _fileCache.Sum(s => s.Value.Definition.Size) / (1024 * 1024 * 1024));

        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Failed to prime cache");
        }
    }

    private void SaveBucketCacheFile(AbsolutePath cacheFile)
    {
        using var file = cacheFile.Open(FileMode.Create, FileAccess.Write);
        using var sw = new StreamWriter(file);
        foreach(var entry in _allObjects)
        {
            sw.WriteLine($"{entry.Key}||{entry.Value}");
        }
    }
    
    private void LoadBucketCacheFile(AbsolutePath cacheFile)
    {
        using var file = cacheFile.Open(FileMode.Open, FileAccess.Read);
        using var sr = new StreamReader(file);
        while (!sr.EndOfStream)
        {
            var line = sr.ReadLine();
            var parts = line!.Split("||");
            _allObjects.TryAdd(parts[0].ToRelativePath(), long.Parse(parts[1]));
        }
    }

    private async Task<FileDefinition> PrimeDefinition(RelativePath name)
    {
        return await CircuitBreaker.WithAutoRetryAllAsync(_logger, async () =>
        {
            var uri = _baseUri + $"{name}/definition.json.gz";
            using var response = await _httpClient.GetAsync(uri);
            return await ReadDefinition(await response.Content.ReadAsStreamAsync());
        });
    }

    private async IAsyncEnumerable<S3Object> AllObjects()
    {
        var sw = Stopwatch.StartNew();
        var total = 0;
        _logger.Log(LogLevel.Information, "Listing all objects in S3");
        var results = await _s3.ListObjectsV2Async(new ListObjectsV2Request()
        {
            BucketName = _bucketName,
        });
        TOP:
        total += results.S3Objects.Count;
        _logger.Log(LogLevel.Information, "Got {S3ObjectsCount} objects, {Total} total", results.S3Objects.Count, total);
        foreach (var result in results.S3Objects)
        {
            yield return result;
        }

        if (results.IsTruncated)
        {
            results = await _s3.ListObjectsV2Async(new ListObjectsV2Request
            {
                ContinuationToken = results.NextContinuationToken,
                BucketName = _bucketName,
            });
            goto TOP;
        }
        _logger.LogInformation("Finished listing all objects in S3 in {Elapsed}", sw.Elapsed);
    }

    public IEnumerable<FileDefinitionMetadata> AllDefinitions => _fileCache.Values;
    
    /// <summary>
    /// Used space in bytes
    /// </summary>
    public long UsedSpace => _fileCache.Sum(s => s.Value.Definition.Size);
    
    public async Task StreamForPart(string mungedName, int part, Func<Stream, Task> func)
    {
        var definition = _fileCache[mungedName].Definition;
        
        if (part >= definition.Parts.Length)
            throw new ArgumentOutOfRangeException(nameof(part));
        
        var uri = _baseUri + $"{mungedName}/parts/{part}";
        using var response = await _httpClient.GetAsync(uri);
        await func(await response.Content.ReadAsStreamAsync());
    }
    
    public async Task WritePart(string mungedName, int part, Stream ms)
    {
        await _s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = mungedName.ToRelativePath().Combine("parts", part.ToString()).ToString().Replace("\\", "/"),
            InputStream = ms,
            DisablePayloadSigning = true,
            ContentType = "application/octet-stream"
        });
    }

    public async Task WriteDefinition(FileDefinition definition)
    {
        await using var ms = new MemoryStream();
        await using (var gz = new GZipStream(ms, CompressionLevel.Optimal, true))
        {
            await _dtos.Serialize(definition, gz);
        }
        ms.Position = 0;
        
        await _s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = definition.MungedName.ToRelativePath().Combine("definition.json.gz").ToString().Replace("\\", "/"),
            InputStream = ms,
            DisablePayloadSigning = true,
            ContentType = "application/octet-stream"
        });
        _fileCache.TryAdd(definition.MungedName, new FileDefinitionMetadata
        {
            Definition = definition,
            Updated = DateTime.UtcNow
        });
        _byServerId.TryAdd(definition.ServerAssignedUniqueId!, definition);
    }

    public async Task<FileDefinition> ReadDefinition(string mungedName)
    {
        return _fileCache[mungedName].Definition;
    }
    
    public bool IsDefinition(string mungedName)
    {
        return _fileCache.ContainsKey(mungedName);
    }
    
    
    private async Task<FileDefinition> ReadDefinition(Stream stream)
    {
        var gz = new GZipStream(stream, CompressionMode.Decompress);
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
        var allFiles = _allObjects.Where(f => f.Key.TopParent.ToString() == definition.MungedName)
            .Select(f => f.Key).ToList();
        foreach (var batch in allFiles.Batch(512))
        {
            var batchedArray = batch.ToHashSet();
            _logger.LogInformation("Deleting {Count} files for prefix {Prefix}", batchedArray.Count, definition.MungedName);
            await _s3.DeleteObjectsAsync(new DeleteObjectsRequest
            {
                BucketName = _bucketName,
                
                Objects = batchedArray.Select(f => new KeyVersion
                {
                    Key = f.ToString().Replace("\\", "/")
                }).ToList()
            });
            foreach (var key in batchedArray)
            {
                _allObjects.TryRemove(key, out _);
            }
        }

        _byServerId.TryRemove(definition.ServerAssignedUniqueId!, out _);
        _fileCache.TryRemove(definition.MungedName, out _);
    }

    public async ValueTask<FileDefinition> ReadDefinitionForServerId(string serverAssignedUniqueId)
    {
        return _byServerId[serverAssignedUniqueId];
    }

    public string DecodeName(string mungedName)
    {
        var decoded = HttpUtility.UrlDecode(mungedName);
        return IsDefinition(decoded) ? decoded : mungedName;
    }
}