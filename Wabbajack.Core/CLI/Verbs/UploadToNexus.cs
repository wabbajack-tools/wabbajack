using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.CLI.Builder;
using Wabbajack.Common;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Networking.NexusApi;
using Wabbajack.Networking.NexusApi.DTOs;
using Wabbajack.Paths;


namespace Wabbajack.CLI.Verbs;

public class UploadToNexus
{
    private readonly ILogger<UploadToNexus> _logger;
    private readonly NexusApi _client;
    private readonly DTOSerializer _dtos;

    public UploadToNexus(ILogger<UploadToNexus> logger, NexusApi wjClient, DTOSerializer dtos)
    {
        _logger = logger;
        _client = wjClient;
        _dtos = dtos;
    }

    public static VerbDefinition Definition = new("upload-to-nexus",
        "Uploads a file to the Nexus defined by the given .json definition file", new[]
        {
            new OptionDefinition(typeof(AbsolutePath), "d", "definition", "Definition JSON file")
        });


    public async Task<int> Run(AbsolutePath definition)
    {
        var d = await definition.FromJson<UploadDefinition>(_dtos);

        await _client.UploadFile(d);
        

        return 0;
    }
    
}