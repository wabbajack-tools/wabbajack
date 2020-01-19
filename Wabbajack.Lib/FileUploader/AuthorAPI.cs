using System;
using System.Net.Http;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using Wabbajack.Common;

namespace Wabbajack.Lib.FileUploader
{
    public class AuthorAPI
    {
        public static IObservable<bool> HaveAuthorAPIKey => Utils.HaveEncryptedJsonObservable("author-api-key");

        public static IObservable<string> AuthorAPIKey => HaveAuthorAPIKey.Where(h => h)
            .Select(_ => File.ReadAllText(Path.Combine(Consts.LocalAppDataPath, "author-api-key")));


        public static string GetAPIKey()
        {
            return File.ReadAllText(Path.Combine(Consts.LocalAppDataPath, "author-api-key.txt")).Trim();
        }
        public static bool HasAPIKey => File.Exists(Path.Combine(Consts.LocalAppDataPath, "author-api-key.txt"));
        
        
        public static readonly Uri UploadURL = new Uri("https://build.wabbajack.org/upload_file");
        public static long BLOCK_SIZE = (long)1024 * 1024 *  8;
        public static Task<string> UploadFile(WorkQueue queue, string filename)
        {
            var tcs = new TaskCompletionSource<string>();
            queue.QueueTask(async () =>
            {
                using (var stream =
                    new StatusFileStream(File.OpenRead(filename), $"Uploading {Path.GetFileName(filename)}", queue))
                {
                    var client = new HttpClient();
                    client.DefaultRequestHeaders.Add("X-API-KEY", AuthorAPI.GetAPIKey());
                    var response = await client.PutAsync(UploadURL+$"/{Path.GetFileName(filename)}/start", new StringContent(""));
                    if (!response.IsSuccessStatusCode)
                    {
                        tcs.SetResult("FAILED");
                        return;
                    }

                    var key = await response.Content.ReadAsStringAsync();

                    var data = new byte[BLOCK_SIZE];
                    while (stream.Position < stream.Length)
                    {
                        var old_offset = stream.Position;

                        var new_size = Math.Min(stream.Length - stream.Position, BLOCK_SIZE);
                        
                        if (new_size != data.Length) 
                            data = new byte[new_size];

                        stream.ReadAsync(data, 0, data.Length);

                        response = await client.PutAsync(UploadURL + $"/{key}/data/{old_offset}",
                            new ByteArrayContent(data));

                        if (!response.IsSuccessStatusCode)
                        {
                            tcs.SetResult("FAILED");
                            return;
                        }

                        var val = long.Parse(await response.Content.ReadAsStringAsync());
                        if (val != old_offset + data.Length)
                        {
                            tcs.SetResult("Sync Error");
                            return;
                        }


                    }

                    response = await client.PutAsync(UploadURL + $"/{key}/finish", new StringContent(""));
                    if (response.IsSuccessStatusCode)
                        tcs.SetResult(await response.Content.ReadAsStringAsync());
                    else 
                        tcs.SetResult("FAILED");
                }
            });
            return tcs.Task;
        }

    }
}
