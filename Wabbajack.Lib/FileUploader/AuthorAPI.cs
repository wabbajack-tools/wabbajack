using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Common;
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
                var handler = new HttpClientHandler {MaxConnectionsPerServer = MAX_CONNECTIONS};
                var client = new HttpClient(handler);
                var fsize = new FileInfo(filename).Length;
                client.DefaultRequestHeaders.Add("X-API-KEY", AuthorAPI.GetAPIKey());
                var response = await client.PutAsync(UploadURL+$"/{Path.GetFileName(filename)}/start", new StringContent(""));
                if (!response.IsSuccessStatusCode)
                {
                    tcs.SetException(new Exception($"Start Error: {response.StatusCode} {response.ReasonPhrase}"));
                    return;
                }

                var key = await response.Content.ReadAsStringAsync();
                long sent = 0;
                using (var iqueue = new WorkQueue(MAX_CONNECTIONS))
                {
                    iqueue.Report("Starting Upload", 1);
                await Enumerable.Range(0, (int)(fsize / BLOCK_SIZE))
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

                            
                            response = await client.PutAsync(UploadURL + $"/{key}/data/{block_offset}",
                                new ByteArrayContent(data));

                            if (!response.IsSuccessStatusCode)
                            {
                                tcs.SetException(new Exception($"Put Error: {response.StatusCode} {response.ReasonPhrase}"));
                                return;
                            }

                            var val = long.Parse(await response.Content.ReadAsStringAsync());
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
                    response = await client.PutAsync(UploadURL + $"/{key}/finish", new StringContent(""));
                    if (response.IsSuccessStatusCode)
                        tcs.SetResult(await response.Content.ReadAsStringAsync());
                    else
                        tcs.SetException(new Exception($"Finalization Error: {response.StatusCode} {response.ReasonPhrase}"));
                }

                progressFn(0.0);

            });
            return tcs.Task;
        }

    }
}
