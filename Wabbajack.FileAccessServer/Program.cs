using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Wabbajack.Common;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var hostPath = args[0].ToAbsolutePath();
var apiKeys = (await args[1].ToAbsolutePath().ReadAllLinesAsync().ToArray()).ToHashSet();

var appendLock = new SemaphoreSlim(1);

app.MapPost("/files", 
        async ([FromQuery(Name = "path")] string path,
                [FromQuery(Name = "method")] Method method,
                [FromHeader(Name = "apikey")] string apikey,
                HttpRequest request,
                CancellationToken token
        ) =>
        {
                var fullPath = path.ToRelativePath().RelativeTo(hostPath);
                if (!fullPath.InFolder(hostPath)) return Results.NotFound();
                if (!apiKeys.Contains(apikey)) return Results.NotFound();

                switch (method)
                {
                        case Method.Read:
                                return Results.File(fullPath.ToString());
                        case Method.Write: 
                                await fullPath.WriteAllAsync(request.Body, token);
                                return Results.Ok();
                        case Method.Append:
                                var data = await request.Body.ReadAllAsync();
                                await appendLock.WaitAsync();
                                try
                                {
                                        await using var f = fullPath.Open(FileMode.Append, FileAccess.Write, FileShare.None);
                                        f.Write(data);
                                }
                                finally
                                {
                                        appendLock.Release();
                                        
                                }
                                return Results.Ok();
                        case Method.Delete:
                                fullPath.Delete();
                                return Results.Ok();
                        case Method.List:
                                return Results.Ok(fullPath.EnumerateFiles()
                                        .Select(f => f.RelativeTo(hostPath).ToString()).ToArray());
                        default:
                                throw new ArgumentOutOfRangeException(nameof(method), method, null);
                }
        }
);

app.Run();

public enum Method
{
        Read,
        Write,
        Append,
        Delete,
        List
}