using System;
using System.IO;
using System.Linq;
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
        public static Task<string> UploadFile(WorkQueue queue, string filename, Action<double> progressFn)
        {
            var tcs = new TaskCompletionSource<string>();
            Task.Run(async () =>
            {
   


                var client = new HttpClient();
                var fsize = new FileInfo(filename).Length;
                client.DefaultRequestHeaders.Add("X-API-KEY", AuthorAPI.GetAPIKey());
                var response = await client.PutAsync(UploadURL+$"/{Path.GetFileName(filename)}/start", new StringContent(""));
                if (!response.IsSuccessStatusCode)
                {
                    tcs.SetResult("FAILED");
                    return;
                }

                var key = await response.Content.ReadAsStringAsync();
                long sent = 0;
                using (var iqueue = new WorkQueue(8))
                {
                    iqueue.Report("Starting Upload", 1);
                await Enumerable.Range(0, (int)(fsize / BLOCK_SIZE))
                    .PMap(iqueue, async block_idx =>
                    {
                        var block_offset = block_idx * BLOCK_SIZE;
                        var block_size = block_offset + BLOCK_SIZE > fsize
                            ? fsize - block_offset
                            : BLOCK_SIZE;
                        Interlocked.Add(ref sent, block_size);
                        progressFn((double)sent / fsize);
                        
                        using (var fs = File.OpenRead(filename))
                        {
                            fs.Position = block_offset;
                            var data = new byte[block_size];
                            await fs.ReadAsync(data, 0, data.Length);

                            response = await client.PutAsync(UploadURL + $"/{key}/data/{block_offset}",
                                new ByteArrayContent(data));

                            if (!response.IsSuccessStatusCode)
                            {
                                tcs.SetResult("FAILED");
                                return;
                            }

                            var val = long.Parse(await response.Content.ReadAsStringAsync());
                            if (val != block_offset + data.Length)
                            {
                                tcs.SetResult("Sync Error");
                                return;
                            }
                        }
                    });
                }

                response = await client.PutAsync(UploadURL + $"/{key}/finish", new StringContent(""));
                if (response.IsSuccessStatusCode)
                    tcs.SetResult(await response.Content.ReadAsStringAsync());
                else 
                    tcs.SetResult("FAILED");

                progressFn(0.0);

            });
            return tcs.Task;
        }

    }
}
