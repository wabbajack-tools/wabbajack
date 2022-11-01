using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.CLI.Builder;
using Wabbajack.Common;
using Wabbajack.Compression.BSA;
using Wabbajack.DTOs;
using Wabbajack.DTOs.BSA.FileStates;
using Wabbajack.DTOs.Directives;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Installer;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;
using Wabbajack.VFS;
using AbsolutePathExtensions = Wabbajack.Common.AbsolutePathExtensions;

namespace Wabbajack.CLI.Verbs;

public class VerifyModlistInstall
{
    private readonly DTOSerializer _dtos;
    private readonly ILogger<VerifyModlistInstall> _logger;
    
    public VerifyModlistInstall(ILogger<VerifyModlistInstall> logger, DTOSerializer dtos, IResource<FileHashCache> limiter)
    {
        _limiter = limiter;
        _logger = logger;
        _dtos = dtos;
    }
    
    public static VerbDefinition Definition = new("verify-modlist-install", "Verify a modlist installed correctly",
        new[]
        {
            new OptionDefinition(typeof(AbsolutePath), "m", "modlistLocation",
                "The .wabbajack file used to install the modlist"),
            new OptionDefinition(typeof(AbsolutePath), "i", "installFolder", "The installation folder of the modlist")
        });

    private readonly IResource<FileHashCache> _limiter;


    public async Task<int> Run(AbsolutePath modlistLocation, AbsolutePath installFolder, CancellationToken token)
    {
        _logger.LogInformation("Loading modlist {ModList}", modlistLocation);
        var list = await StandardInstaller.LoadFromFile(_dtos, modlistLocation);
        
        _logger.LogInformation("Indexing files");
        var byTo = list.Directives.ToDictionary(d => d.To);


        _logger.LogInformation("Scanning files");
        var errors = await list.Directives.PMapAllBatchedAsync(_limiter, async directive => 
            {
                if (!(directive is CreateBSA || directive.IsDeterministic))
                    return null;
                
                if (directive.To.InFolder(Consts.BSACreationDir))
                    return null;

                var dest = directive.To.RelativeTo(installFolder);
                if (!dest.FileExists())
                {
                    return new Result
                    {
                        Path = directive.To,
                        Message = $"File does not exist directive {directive.GetType()}"
                    };
                }

                if (Consts.KnownModifiedFiles.Contains(directive.To.FileName))
                    return null;

                if (directive is CreateBSA bsa)
                {
                    return await VerifyBSA(dest, bsa, byTo, token);
                }

                if (dest.Size() != directive.Size)
                {
                    return new Result
                    {
                        Path = directive.To,
                        Message = $"Sizes do not match got {dest.Size()} expected {directive.Size}"
                    };
                }

                if (directive.Size > (1024 * 1024 * 128))
                {
                    _logger.LogInformation("Hashing {Size} file at {Path}", directive.Size.ToFileSizeString(),
                        directive.To);
                }

                var hash = await AbsolutePathExtensions.Hash(dest, token);
                if (hash != directive.Hash)
                {
                    return new Result
                    {
                        Path = directive.To,
                        Message = $"Hashes do not match, got {hash} expected {directive.Hash}"
                    };
                }

                return null;
        }).Where(r => r != null)
            .ToList();
        
        _logger.LogInformation("Found {Count} errors", errors.Count);


        foreach (var error in errors)
        {
            _logger.LogError("{File} | {Message}", error.Path, error.Message);
        }


        return 0;
    }

    private async Task<Result?> VerifyBSA(AbsolutePath dest, CreateBSA bsa, Dictionary<RelativePath, Directive> byTo, CancellationToken token)
    {
        _logger.LogInformation("Verifying Created BSA {To}", bsa.To);
        var archive = await BSADispatch.Open(dest);
        var filesIndexed = archive.Files.ToDictionary(d => d.Path);
        
        if (dest.Extension == Ext.Bsa && dest.Size() >= 1024L * 1024 * 1024 * 2)
        {
            return new Result()
            {
                Path = bsa.To,
                Message = $"BSA is over 2GB in size, this will cause crashes : {bsa.To}"
            };
        }

        foreach (var file in bsa.FileStates)
        {
            if (file is BA2DX10File) continue;
            var state = filesIndexed[file.Path];
            var sf = await state.GetStreamFactory(token);
            await using var stream = await sf.GetStream();
            var hash = await stream.Hash(token);

            var astate = bsa.FileStates.First(f => f.Path == state.Path);
            var srcDirective = byTo[Consts.BSACreationDir.Combine(bsa.TempID, astate.Path)];

            if (!srcDirective.IsDeterministic)
                continue;

            if (srcDirective.Hash != hash)
            {
                return new Result
                {
                    Path = bsa.To,
                    Message =
                        $"BSA {bsa.To} contents do not match at {file.Path} got {hash} expected {srcDirective.Hash}"
                };
            }
        }

        
        return null;
    }

    public class Result
    {
        public RelativePath Path { get; set; }
        public string Message { get; set; }
    }
}