using System;
using System.Net;
using System.Reactive.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using Wabbajack.Common;
using Wabbajack.Common.StatusFeed;
using Wabbajack.Lib.Downloaders;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Wabbajack.Test
{
    public class CrappyRandomServer : SimpleHTTPServer
    {
        private Random _random;
        private readonly byte[] _data;

        public byte[] Data => _data;

        public CrappyRandomServer() : base("")
        {
            _random = new Random();
            _data = new byte[_random.Next(1024 * 1024, 1024 * 2048)];
            _random.NextBytes(_data);
        }

        protected override void Process(HttpListenerContext context)
        {
            context.Response.ContentType = "application/octet-stream";
            context.Response.ContentLength64 = _data.Length;
            context.Response.AddHeader("Accept-Ranges", "bytes");
            context.Response.StatusCode = (int)HttpStatusCode.OK;

            var range = context.Request.Headers.Get("Range");
            int start = 0;
            if (range != null)
            {
                var match = new Regex("(?<=bytes=)[0-9]+(?=\\-)").Match(range);
                if (match != null)
                {
                    start = int.Parse(match.ToString());
                }
            }
            var end = Math.Min(start + _random.Next(1024 * 32, 1024 * 64), _data.Length);
            context.Response.OutputStream.Write(_data, start, end - start);
            context.Response.OutputStream.Flush();
            Thread.Sleep(500);
            context.Response.Abort();
        }
    }

    public class RestartingDownloadsTests
    {
        private ITestOutputHelper TestContext { get; set; }

        public RestartingDownloadsTests(ITestOutputHelper helper)
        {
            TestContext = helper;
            Utils.LogMessages.OfType<IInfo>().Subscribe(onNext: msg => TestContext.WriteLine(msg.ShortDescription));
            Utils.LogMessages.OfType<IUserIntervention>().Subscribe(msg =>
                TestContext.WriteLine("ERROR: User intervention required: " + msg.ShortDescription));
        }

        [Fact]
        public async Task DownloadResume()
        {
            using var testFile = new TempFile();
            using var server = new CrappyRandomServer();
            var state = new HTTPDownloader.State {Url = $"http://localhost:{server.Port}/foo"};
                
            await state.Download(testFile.Path);

            Assert.Equal(server.Data, await testFile.Path.ReadAllBytesAsync());

            testFile.Path.Delete();
        }
    }




}
