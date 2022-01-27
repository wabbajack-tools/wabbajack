using System.Diagnostics;
using System.Text.Json;
using Wabbajack.DTOs.BrowserMessages;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Paths;
using Wabbajack.RateLimiter;

namespace Wabbajack.Networking.Browser;


public class Client
{
    private readonly Configuration _config;
    private readonly DTOSerializer _dtos;

    public Client(Configuration config, DTOSerializer dtos)
    {
        _config = config;
        _dtos = dtos;
    }

    public async Task ManualDownload(string prompt, Uri uri, AbsolutePath dest, CancellationToken token, IJob job)
    {
        var process = new Process()
        {
            StartInfo = new ProcessStartInfo()
            {
                FileName = _config.HostExecutable.ToString(),
                RedirectStandardError = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true
            }
        };

        var ptask = process.Start();

        var reader = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                var msg = await JsonSerializer.DeserializeAsync<IMessage>(process.StandardOutput.BaseStream,
                    _dtos.Options, token);
                if (msg is DownloadProgress dp)
                {
                    job.ReportNoWait((int) dp.BytesCompleted);
                    job.Size = dp.ExpectedSize;
                    if (dp.IsDone)
                    {
                        return;
                    }
                }
            }
        }, token);

        await process.StandardInput.WriteAsync(JsonSerializer.Serialize(new DTOs.BrowserMessages.ManualDownload()
        {
            Prompt = prompt,
            Url = uri,
            Path = dest
        }));

        await process.WaitForExitAsync(token);
        
    }
}