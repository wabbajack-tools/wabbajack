using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using GraphQL.Client;
using GraphQL.Client.Http;
using GraphQL.Common.Request;
using Wabbajack.Common;
using Wabbajack.Lib.FileUploader;
using Wabbajack.Lib.GraphQL.DTOs;
using Path = Alphaleonis.Win32.Filesystem.Path;

namespace Wabbajack.Lib.GraphQL
{
    public class GraphQLService
    {
        public static readonly Uri BaseURL = new Uri("https://build.wabbajack.org/graphql");
        public static readonly Uri UploadURL = new Uri("https://build.wabbajack.org/upload_file");
        
        public static async Task<List<UploadedFile>> GetUploadedFiles()
        {
            var client = new GraphQLHttpClient(BaseURL);
            var query = new GraphQLRequest
            {
                Query = @"
                   query uploadedFilesQuery {
                         uploadedFiles {
                            id
                            name
                            hash
                            uri
                            uploader
                            uploadDate
                          }
                   }"
            };
            var result = await client.SendQueryAsync(query);
            return result.GetDataFieldAs<List<UploadedFile>>("uploadedFiles");
        }

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
                    var content = new StreamContent(stream);
                    var form = new MultipartFormDataContent
                    {
                        {content, "files", Path.GetFileName(filename)}
                    };
                    content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

                    var response = await client.PostAsync(UploadURL, form);
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
