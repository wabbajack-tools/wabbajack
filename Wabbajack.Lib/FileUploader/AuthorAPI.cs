using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.Lib.Downloaders;
using File = Alphaleonis.Win32.Filesystem.File;
using Path = Alphaleonis.Win32.Filesystem.Path;

namespace Wabbajack.Lib.FileUploader
{
    public class AuthorAPI
    {
        public static IObservable<bool> HaveAuthorAPIKey => Utils.HaveEncryptedJsonObservable(Consts.AuthorAPIKeyFile);

        public static string? ApiKeyOverride = null;

        public static async Task<string> GetAPIKey(string? apiKey = null)
        {
            if (ApiKeyOverride != null) return ApiKeyOverride;
            return apiKey ?? (await Consts.LocalAppDataPath.Combine(Consts.AuthorAPIKeyFile).ReadAllTextAsync()).Trim();
        }
        
        public static Uri UploadURL => new Uri($"{Consts.WabbajackBuildServerUri}upload_file");
        public static long BLOCK_SIZE = (long)1024 * 1024 * 2;
        public static int MAX_CONNECTIONS = 8;
        public static Task<string> UploadFile(AbsolutePath filename, Action<double> progressFn, string? apikey = null)
        {
            var tcs = new TaskCompletionSource<string>();
            Task.Run(async () =>
            {
                var client = await GetAuthorizedClient(apikey);

                var fsize = filename.Size;
                var hashTask = filename.FileHashAsync();

                Utils.Log($"{UploadURL}/{filename.FileName.ToString()}/start");
                using var response = await client.PutAsync($"{UploadURL}/{filename.FileName.ToString()}/start", new StringContent(""));
                if (!response.IsSuccessStatusCode)
                {
                    Utils.Log("Error starting upload");
                    Utils.Log(await response.Content.ReadAsStringAsync());
                    tcs.SetException(new Exception($"Start Error: {response.StatusCode} {response.ReasonPhrase}"));
                    return;
                }

                IEnumerable<long> Blocks(long fsize)
                {
                    for (long block = 0; block * BLOCK_SIZE < fsize; block ++)
                        yield return block;
                }

                var key = await response.Content.ReadAsStringAsync();
                long sent = 0;
                using (var iqueue = new WorkQueue(MAX_CONNECTIONS))
                {
                    iqueue.Report("Starting Upload", Percent.One);
                await Blocks(fsize)
                    .PMap(iqueue, async blockIdx =>
                    {
                        if (tcs.Task.IsFaulted) return;
                        var blockOffset = blockIdx * BLOCK_SIZE;
                        var blockSize = blockOffset + BLOCK_SIZE > fsize
                            ? fsize - blockOffset
                            : BLOCK_SIZE;
                        Interlocked.Add(ref sent, blockSize);
                        progressFn((double)sent / fsize);

                        var data = new byte[blockSize];
                        await using (var fs = filename.OpenRead())
                        {
                            fs.Position = blockOffset;
                            await fs.ReadAsync(data, 0, data.Length);
                        }


                        var offsetResponse = await client.PutAsync(UploadURL + $"/{key}/data/{blockOffset}",
                            new ByteArrayContent(data));

                        if (!offsetResponse.IsSuccessStatusCode)
                        {
                            Utils.Log(await offsetResponse.Content.ReadAsStringAsync());
                            tcs.SetException(new Exception($"Put Error: {offsetResponse.StatusCode} {offsetResponse.ReasonPhrase}"));
                            return;
                        }

                        var val = long.Parse(await offsetResponse.Content.ReadAsStringAsync());
                        if (val != blockOffset + data.Length)
                        {
                            tcs.SetResult($"Sync Error {val} vs {blockOffset + data.Length} Offset {blockOffset} Size {data.Length}");
                            tcs.SetException(new Exception($"Sync Error {val} vs {blockOffset + data.Length}"));
                        }
                    });
                }

                if (!tcs.Task.IsFaulted)
                {
                    progressFn(1.0);
                    var hash = (await hashTask).ToHex();
                    using var finalResponse = await client.PutAsync(UploadURL + $"/{key}/finish/{hash}", new StringContent(""));
                    if (finalResponse.IsSuccessStatusCode)
                        tcs.SetResult(await finalResponse.Content.ReadAsStringAsync());
                    else
                    {
                        Utils.Log("Finalization Error: ");
                        Utils.Log(await finalResponse.Content.ReadAsStringAsync());
                        tcs.SetException(new Exception(
                            $"Finalization Error: {finalResponse.StatusCode} {finalResponse.ReasonPhrase}"));
                    }
                }

                progressFn(0.0);

            });
            return tcs.Task;
        }

        public static async Task<Common.Http.Client> GetAuthorizedClient(string? apiKey = null)
        {
            var client = new Common.Http.Client();
            client.Headers.Add(("X-API-KEY", await GetAPIKey(apiKey)));
            return client;
        }
        
        public static async Task<string> RunJob(string jobtype)
        {
            var client = await GetAuthorizedClient();
            return await client.GetStringAsync($"{Consts.WabbajackBuildServerUri}jobs/enqueue_job/{jobtype}");
            
        }

        public static async Task<string> UpdateNexusCache()
        {
            return await RunJob("GetNexusUpdatesJob");
        }

        public static async Task<string> UpdateServerModLists()
        {
            return await RunJob("UpdateModLists");
        }

        public static async Task<bool> UploadPackagedInis(IEnumerable<Archive> archives)
        {
            archives = archives.ToArray(); // defensive copy
            Utils.Log($"Packaging {archives.Count()} inis");
            try
            {
                await using var ms = new MemoryStream();
                using (var z = new ZipArchive(ms, ZipArchiveMode.Create, true))
                {
                    foreach (var e in archives)
                    {
                        if (e.State == null) continue;
                        var entry = z.CreateEntry(Path.GetFileName(e.Name));
                        await using var os = entry.Open();
                        await os.WriteAsync(Encoding.UTF8.GetBytes(string.Join("\n", e.State.GetMetaIni())));
                    }
                }

                var client = new Common.Http.Client();
                var response = await client.PostAsync($"{Consts.WabbajackBuildServerUri}indexed_files/notify", new ByteArrayContent(ms.ToArray()));
                
                if (response.IsSuccessStatusCode) return true;

                Utils.Log("Error sending Inis");
                Utils.Log(await response.Content.ReadAsStringAsync());
                return false;

            }
            catch (Exception ex)
            {
                Utils.Log(ex.ToString());
                return false;
            }
        }

        public static async Task<string> GetServerLog()
        {
            return await (await GetAuthorizedClient()).GetStringAsync($"{Consts.WabbajackBuildServerUri}heartbeat/logs");
        }

        public static async Task<IEnumerable<string>> GetMyFiles()
        {
            return (await (await GetAuthorizedClient()).GetStringAsync($"{Consts.WabbajackBuildServerUri}uploaded_files/list")).FromJsonString<string[]>();
        }

        public static async Task<string> DeleteFile(string name)
        {
            var result = await (await GetAuthorizedClient())
                .DeleteStringAsync($"{Consts.WabbajackBuildServerUri}uploaded_files/{name}");
            return result;
        }
    }
}
