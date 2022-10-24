using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.CLI.Builder;
using Wabbajack.DTOs.Directives;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Installer;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.CLI.Verbs;

public class VerifyModlistInstall
{
    private readonly DTOSerializer _dtos;
    private readonly ILogger<VerifyModlistInstall> _logger;
    
    public VerifyModlistInstall(ILogger<VerifyModlistInstall> logger, DTOSerializer dtos)
    {
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



    public async Task<int> Run(AbsolutePath modlistLocation, AbsolutePath installFolder)
    {
        _logger.LogInformation("Loading modlist {ModList}", modlistLocation);
        var list = await StandardInstaller.LoadFromFile(_dtos, modlistLocation);

        var errors = new List<Result>();

        _logger.LogInformation("Scanning files");
        foreach (var directive in list.Directives)
        {
            if (directive is ArchiveMeta)
                continue;
            
            if (directive is RemappedInlineFile)
                continue;
            
            if (directive.To.InFolder(Consts.BSACreationDir))
                continue;
            
            var dest = directive.To.RelativeTo(installFolder);
            if (!dest.FileExists())
            {
                errors.Add(new Result
                {
                    Path = directive.To,
                    Message = $"File does not exist directive {directive.GetType()}"
                });
                continue;
            }
            if (dest.Size() != directive.Size)
            {
                errors.Add(new Result
                {
                    Path = directive.To,
                    Message = $"Sizes do not match got {dest.Size()} expected {directive.Size}"
                });
            }
            
        }
        
        _logger.LogInformation("Found {Count} errors", errors.Count);


        foreach (var error in errors)
        {
            _logger.LogError("{File} | {Message}", error.Path, error.Message);
        }


        return 0;
    }

    public class Result
    {
        public RelativePath Path { get; set; }
        public string Message { get; set; }
    }
}