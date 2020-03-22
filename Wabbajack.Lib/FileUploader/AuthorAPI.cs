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
        public static IObservable<bool> HaveAuthorAPIKey => Utils.HaveEncryptedJsonObservable("author-api-key.txt");

        public static IObservable<string> AuthorAPIKey => HaveAuthorAPIKey.Where(h => h)
            .Select(_ => File.ReadAllText(Path.Combine(Consts.LocalAppDataPath, "author-api-key.txt")));


        public static string GetAPIKey()
        {
            return File.ReadAllText(Path.Combine(Consts.LocalAppDataPath, "author-api-key.txt")).Trim();
        }
        public static bool HasAPIKey => File.Exists(Path.Combine(Consts.LocalAppDataPath, "author-api-key.txt"));
        
        
        public static readonly Uri UploadURL = new Uri("https://build.wabbajack.org/upload_file");
        public static long BLOCK_SIZE = (long)1024 * 1024 * 2;
        public static int MAX_CONNECTIONS = 8;
        public static Task<string> UploadFile(WorkQueue queue, string filename, Action<double> progressFn)
        {
            var tcs = new TaskCompletionSource<string>();
            Task.Run(async () =>
            {
                var client = GetAuthorizedClient();

                var fsize = new FileInfo(filename).Length;
                var hash_task = filename.FileHashAsync();

                var response = await client.PutAsync(UploadURL+$"/{Path.GetFileName(filename)}/start", new StringContent(""));
                if (!response.IsSuccessStatusCode)
                {
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
                    .PMap(iqueue, async block_idx =>
                    {
                        if (tcs.Task.IsFaulted) return;
                        var block_offset = block_idx * BLOCK_SIZE;
                        var block_size = block_offset + BLOCK_SIZE > fsize
                            ? fsize - block_offset
                            : BLOCK_SIZE;
                        Interlocked.Add(ref sent, block_size);
                        progressFn((double)sent / fsize);

                        int retries = 0;
                        
                        using (var fs = File.OpenRead(filename))
                        {
                            fs.Position = block_offset;
                            var data = new byte[block_size];
                            await fs.ReadAsync(data, 0, data.Length);

                            
                            var putResponse = await client.PutAsync(UploadURL + $"/{key}/data/{block_offset}",
                                new ByteArrayContent(data));

                            if (!putResponse.IsSuccessStatusCode)
                            {
                                tcs.SetException(new Exception($"Put Error: {putResponse.StatusCode} {putResponse.ReasonPhrase}"));
                                return;
                            }

                            var val = long.Parse(await putResponse.Content.ReadAsStringAsync());
                            if (val != block_offset + data.Length)
                            {
                                tcs.SetResult($"Sync Error {val} vs {block_offset + data.Length}");
                                tcs.SetException(new Exception($"Sync Error {val} vs {block_offset + data.Length}"));
                            }
                        }
                    });
                }

                if (!tcs.Task.IsFaulted)
                {
                    progressFn(1.0);
                    var hash = (await hash_task).ToHex();
                    response = await client.PutAsync(UploadURL + $"/{key}/finish/{hash}", new StringContent(""));
                    if (response.IsSuccessStatusCode)
                        tcs.SetResult(await response.Content.ReadAsStringAsync());
                    else
                        tcs.SetException(new Exception($"Finalization Error: {response.StatusCode} {response.ReasonPhrase}"));
                }

                progressFn(0.0);

            });
            return tcs.Task;
        }

        public static Common.Http.Client GetAuthorizedClient()
        {
            var client = new Common.Http.Client();
            client.Headers.Add(("X-API-KEY", GetAPIKey()));
            return client;
        }
        
        public static async Task<string> RunJob(string jobtype)
        {
            var client = GetAuthorizedClient();
            return await client.GetStringAsync($"https://{Consts.WabbajackCacheHostname}/jobs/enqueue_job/{jobtype}");
            
        }

        public static async Task<string> UpdateNexusCache()
        {
            return await RunJob("GetNexusUpdatesJob");
        }

        public static async Task<string> UpdateServerModLists()
        {
            return await RunJob("UpdateModLists");
        }

        public static async Task UploadPackagedInis(WorkQueue queue, IEnumerable<Archive> archives)
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

                var webClient = new WebClient();
                await webClient.UploadDataTaskAsync($"https://{Consts.WabbajackCacheHostname}/indexed_files/notify",
                    "POST", ms.ToArray());
            }
            catch (Exception ex)
            {
                Utils.Log(ex.ToString());
            }
        }

        public static async Task<string> GetServerLog()
        {
            return await GetAuthorizedClient().GetStringAsync($"https://{Consts.WabbajackCacheHostname}/heartbeat/logs");
        }

        public static async Task<IEnumerable<string>> GetMyFiles()
        {
            return (await GetAuthorizedClient().GetStringAsync($"https://{Consts.WabbajackCacheHostname}/uploaded_files/list")).FromJSONString<string[]>();
        }

        public static async Task<string> DeleteFile(string name)
        {
            var result = await GetAuthorizedClient()
                .DeleteStringAsync($"https://{Consts.WabbajackCacheHostname}/uploaded_files/{name}");
            return result;
        }
    }
}
